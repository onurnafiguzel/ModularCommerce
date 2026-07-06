using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;
using StackExchange.Redis;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;

public sealed class RedisLockReservationStrategy(
    InventoryDbContext context,
    IDistributedLock distributedLock,
    IConfiguration configuration,
    ILogger<RedisLockReservationStrategy> logger) : IReservationStrategy
{
    public async Task<Result<Reservation>> ReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromSeconds(configuration.GetValue("Inventory:RedisLock:TtlSeconds", 5));
        var waitBudget = TimeSpan.FromMilliseconds(configuration.GetValue("Inventory:RedisLock:WaitBudgetMs", 100));
        var lockKey = $"inventory:lock:stock:{productId}";

        ILockHandle? handle;
        try
        {
            handle = await distributedLock.TryAcquireAsync(lockKey, ttl, waitBudget, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {         
            logger.LogError(ex, "Redis kilit servisine ulaşılamadı: {ProductId}", productId);
            return Result.Failure<Reservation>(InventoryErrors.LockUnavailable);
        }

        if (handle is null)
        {
            logger.LogWarning(
                "Kilit bekleme bütçesi doldu: {ProductId} ({WaitBudgetMs} ms)",
                productId, waitBudget.TotalMilliseconds);
            return Result.Failure<Reservation>(InventoryErrors.LockTimeout);
        }

        await using (handle)
        {
            logger.LogInformation(
                "Kilit alındı: {ProductId} (bekleme {WaitedMs} ms)",
                productId, (int)handle.WaitedFor.TotalMilliseconds);

            var stockItem = await context.StockItems
                .FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

            if (stockItem is null)
            {
                return Result.Failure<Reservation>(InventoryErrors.StockItemNotFound(productId));
            }

            var reserved = stockItem.Reserve(quantity);
            if (reserved.IsFailure)
            {
                return Result.Failure<Reservation>(reserved.Error);
            }

            context.Reservations.Add(reserved.Value);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Kilit altında beklenmez — TTL kritik bölgede dolduysa xmin (savunma hattı 2) yakalar.
                logger.LogWarning(
                    "Kilit altında versiyon çakışması: {ProductId} — TTL/kritik bölge süresini gözden geçir",
                    productId);
                return Result.Failure<Reservation>(InventoryErrors.ConcurrencyConflict);
            }

            return Result.Success(reserved.Value);
        }
    }
}
