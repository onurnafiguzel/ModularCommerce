using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;
public sealed record OrderPaid(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredOnUtc) : IDomainEvent;
