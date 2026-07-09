namespace ModularCommerce.Ordering.Contracts.IntegrationEvents;
public sealed record OrderCancelled(
    Guid OrderId,
    Guid CustomerId,
    DateTime OccurredOnUtc);
