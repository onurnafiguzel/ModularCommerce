namespace ModularCommerce.Ordering.Contracts.IntegrationEvents;
public sealed record OrderPaid(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredOnUtc);
