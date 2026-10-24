using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.Persistence.Configurations;
using ModularCommerce.Shared.Kernel;
using Npgsql;

namespace ModularCommerce.Ordering.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(OrderingDbContext context) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    public Task<Order?> GetByIdempotencyKeyAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        // AsNoTracking: yarış sonrası kazananı çekerken tracker'da başarısız
        // SaveChanges'ten kalan zehirli durum yeniden kaydedilmeye çalışılmasın.
        => context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey,
                cancellationToken);
    public async Task<Result> AddAsync(Order order, CancellationToken cancellationToken)
    {
        context.Orders.Add(order);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } pg
            && (pg.ConstraintName == OrderConfiguration.IdempotencyIndexName
                || pg.ConstraintName == OrderConfiguration.PrimaryKeyName))
        {
            // FR-5.4 yarışının kapandığı yer: iki eşzamanlı aynı-key checkout'tan ikincisini
            // index durdurur; handler kazananın siparişini döndürür. Order.Id artık
            // (CustomerId, IdempotencyKey)'den DETERMİNİSTİK üretildiğinden (bkz.
            // DeterministicOrderId) bu yarış PK'yi de ihlal edebilir — ikisi de AYNI olayı
            // (aynı key, ikinci deneme) temsil eder, ikisi de aynı şekilde ele alınır.
            return Result.Failure(OrderErrors.DuplicateIdempotencyKey);
        }
    }

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {        
        // interceptor OrderCancelled'ı aynı transaction'da outbox'a yazar (atomik).
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
