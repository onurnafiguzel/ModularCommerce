using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>Raise edilir ama dispatch edilmez — outbox Hafta 8 (Contracts'a o zaman taşınır).</summary>
public sealed record PaymentFailed(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    string FailureCode,
    DateTime OccurredOnUtc) : IDomainEvent;
