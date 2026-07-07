using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;
public sealed record OrderStatusChanged(
    Guid OrderId,
    OrderStatus FromStatus,
    OrderStatus ToStatus,
    DateTime OccurredOnUtc) : IDomainEvent;
