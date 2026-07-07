using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Order?> GetByIdempotencyKeyAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<Result> AddAsync(Order order, CancellationToken cancellationToken);
}
