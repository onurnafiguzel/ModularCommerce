using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModularCommerce.Catalog.Infrastructure.Persistence;

namespace ModularCommerce.Catalog.Infrastructure.Outbox;

/// <summary>
/// SRP: outbox satırlarını broker'a taşır (poll → publish → mark). At-least-once: publish sonrası
/// mark'tan önce crash olursa mesaj tekrar yayınlanabilir — tüketici idempotent olmalı. FIFO
/// OccurredOnUtc. (Ordering.OutboxDispatcher'ın CatalogDbContext'e bağlı kopyası; dispatcher generic
/// olmadığından modül-lokal replikasyon gerekir.)
/// </summary>
public sealed class CatalogOutboxDispatcher(
    IServiceProvider serviceProvider,
    ILogger<CatalogOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 20;
    private const int MaxRetries = 10; // zehirli mesaj koruması
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Döngü ASLA ölmemeli: RabbitMQ kopması / geçici hata bir sonraki tur'da düzelir;
                // bekleyen satırlar tabloda durur (outbox'ın dayanıklılığı).
                logger.LogError(ex, "Catalog outbox dispatch turu başarısız; bir sonraki poll'de yeniden denenecek");
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

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var mapper = scope.ServiceProvider.GetRequiredService<IIntegrationEventMapper>();

        await ProcessBatchAsync(db, publisher, mapper, cancellationToken);
    }

    /// <summary>
    /// Batch işleme çekirdeği — bağımlılıkları PARAMETRE olarak alır (DIP), böylece test scope/DI
    /// kurmadan doğrudan çağırabilir.
    /// </summary>
    internal async Task ProcessBatchAsync(
        CatalogDbContext db,
        IPublishEndpoint publisher,
        IIntegrationEventMapper mapper,
        CancellationToken cancellationToken)
    {
        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return;
        }

        foreach (var message in batch)
        {
            var clrType = mapper.ResolveType(message.Type);
            if (clrType is null)
            {
                message.Error = $"Bilinmeyen integration event tipi: {message.Type}";
                message.RetryCount++;
                continue;
            }

            try
            {
                var integrationEvent = JsonSerializer.Deserialize(message.Content, clrType, JsonOptions)!;

                // Tip-silinmiş publish: registry CLR tipini verir, MassTransit doğru exchange'e yönlendirir.
                await publisher.Publish(integrationEvent, clrType, cancellationToken);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                message.RetryCount++;
                logger.LogWarning(ex,
                    "Catalog outbox mesajı publish edilemedi: {MessageId} (deneme {RetryCount}/{Max})",
                    message.Id, message.RetryCount, MaxRetries);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
