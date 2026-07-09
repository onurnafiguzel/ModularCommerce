using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Infrastructure.ContractAdapters;
public sealed class StockReservationService(
    InventoryDbContext context,
    IReservationStrategy strategy,
    ILogger<StockReservationService> logger) : IStockReservationService
{
    private const int MaxReleaseAttempts = 10;

    public async Task<Result<StockReservationDto>> ReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var result = await strategy.ReserveAsync(productId, quantity, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<StockReservationDto>(result.Error);
        }

        var reservation = result.Value;
        return Result.Success(new StockReservationDto(
            reservation.Id,
            reservation.ProductId,
            reservation.Quantity,
            reservation.ExpiresAtUtc));
    }

    public Task<Result> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            reservationId,
            static (stockItem, reservation) => stockItem.Release(reservation),
            "Release",
            cancellationToken);

    public Task<Result> CommitAsync(Guid reservationId, CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            reservationId,
            static (stockItem, reservation) => stockItem.Commit(reservation),
            "Commit",
            cancellationToken);

    public Task<Result> ExpireAsync(Guid reservationId, CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            reservationId,
            static (stockItem, reservation) => stockItem.Expire(reservation),
            "Expire",
            cancellationToken);

    public Task<Result> ReturnAsync(Guid reservationId, CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            reservationId,
            static (stockItem, reservation) => stockItem.Return(reservation),
            "Return",
            cancellationToken);

    private async Task<Result> ExecuteWithRetryAsync(
        Guid reservationId,
        Func<StockItem, Reservation, Result> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxReleaseAttempts; attempt++)
        {
            // Aynı scoped context'te az önce BAŞARISIZ olmuş bir reserve, tracker'da
            // kaydedilmemiş kirli durum bırakmış olabilir (artmış Reserved + eklenmiş
            // Reservation). Her deneme temiz tracker'la başlar; retry'da da güncel
            // xmin'le yeniden yüklemeyi aynı Clear sağlar.
            context.ChangeTracker.Clear();

            var reservation = await context.Reservations
                .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

            if (reservation is null)
            {
                return Result.Failure(InventoryErrors.ReservationNotFound(reservationId));
            }

            var stockItem = await context.StockItems
                .FirstOrDefaultAsync(s => s.Id == reservation.StockItemId, cancellationToken);

            if (stockItem is null)
            {
                return Result.Failure(InventoryErrors.StockItemNotFound(reservation.ProductId));
            }

            var result = operation(stockItem, reservation);
            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogWarning(
                    "{Operation} xmin çakışması, yeniden denenecek: {ReservationId} (deneme {Attempt}/{Max})",
                    operationName, reservationId, attempt, MaxReleaseAttempts);

                // Thundering-herd kırıcı jitter (RedisDistributedLock ile aynı ders).
                await Task.Delay(Random.Shared.Next(5, 16), cancellationToken);
            }
        }

        // Sıcak satırda tüm denemeler kaybedildi: çağıran Warning loglar, iz W9
        // TTL süpürücüsüne kalır (rezervasyon Active kaldı, kaybolmadı).
        return Result.Failure(InventoryErrors.ConcurrencyConflict);
    }
}
