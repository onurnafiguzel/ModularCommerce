using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>Raise edilir ama dispatch edilmez — outbox Hafta 8 (Contracts'a o zaman taşınır).</summary>
public sealed record PaymentCompleted(
    Guid PaymentId,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    DateTime OccurredOnUtc) : IDomainEvent;
