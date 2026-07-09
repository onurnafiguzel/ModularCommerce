using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>Raise edilir ama dispatch edilmez — outbox/integration event W10 (Payment tarafı).</summary>
public sealed record PaymentRefunded(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    DateTime OccurredOnUtc) : IDomainEvent;
