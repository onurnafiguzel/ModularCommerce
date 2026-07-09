using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.Ordering.Contracts;

namespace ModularCommerce.Inventory.Infrastructure.BackgroundJobs;
public sealed class ReservationTtlSweeper(
    IServiceProvider serviceProvider,
    ILogger<ReservationTtlSweeper> logger) : BackgroundService
{
    // TTL 5 dk olduğundan sık poll gerekmez; expire gecikme toleransı yüksek.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Döngü ASLA ölmemeli; sonraki poll'de yeniden denenir.
                logger.LogError(ex, "TTL sweep turu başarısız; bir sonraki poll'de yeniden denenecek");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // graceful shutdown
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var reservations = scope.ServiceProvider.GetRequiredService<IStockReservationService>();
        var reconciler = scope.ServiceProvider.GetRequiredService<IOrderReservationReconciler>();

        await SweepBatchAsync(db, reservations, reconciler, cancellationToken);
    }

    /// <summary>
    /// Batch çekirdeği — bağımlılıkları PARAMETRE olarak alır (DIP), scope/DI kurmadan test
    /// edilebilir. ExecuteAsync bunu scope içinden besler.
    /// </summary>
    internal async Task SweepBatchAsync(
        InventoryDbContext db,
        IStockReservationService reservations,
        IOrderReservationReconciler reconciler,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var candidateIds = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAtUtc < now)
            .OrderBy(r => r.ExpiresAtUtc)
            .Select(r => r.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return;
        }

        // P2 penceresi: Paid siparişe bağlı rezervasyonu Commit'e, yetimi Expire'a ayır.
        // Sınıflandırma hatası fırlarsa yakalanmaz → batch'e dokunulmadan tur biter (D9).
        var classifications = await reconciler.ClassifyAsync(candidateIds, cancellationToken);

        var committed = 0;
        var expired = 0;

        foreach (var classification in classifications)
        {
            if (classification.IsBoundToLiveOrder)
            {
                // P2 RECONCILE: satılmış stoğu kalıcı düş — ASLA expire etme (oversell=0).
                var commit = await reservations.CommitAsync(classification.ReservationId, cancellationToken);
                if (commit.IsSuccess)
                {
                    committed++;
                }
                else
                {
                    logger.LogWarning(
                        "TTL reconcile-commit başarısız: {ReservationId} ({ErrorCode})",
                        classification.ReservationId, commit.Error.Code);
                }
            }
            else
            {
                // Yetim rezervasyon: TTL doldu, stok iade edilir (FR-3.3 compensation).
                var expire = await reservations.ExpireAsync(classification.ReservationId, cancellationToken);
                if (expire.IsSuccess)
                {
                    expired++;
                }
                else
                {
                    logger.LogWarning(
                        "TTL expire başarısız: {ReservationId} ({ErrorCode})",
                        classification.ReservationId, expire.Error.Code);
                }
            }
        }

        logger.LogInformation(
            "TTL sweep: {Candidates} aday, {Committed} reconcile-commit, {Expired} expire",
            candidateIds.Count, committed, expired);
    }
}
