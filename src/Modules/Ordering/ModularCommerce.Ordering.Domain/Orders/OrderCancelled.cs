using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;
public sealed record OrderCancelled(
    Guid OrderId,
    Guid CustomerId,
    DateTime OccurredOnUtc) : IDomainEvent;
