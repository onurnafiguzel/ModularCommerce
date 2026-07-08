namespace ModularCommerce.Payment.Contracts;

/// <summary>Ödeme isteği; IdempotencyKey müşteri kapsamlıdır (checkout key'i aynen taşınır).</summary>
public sealed record ChargeRequest(
    Guid CustomerId,
    Guid OrderId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    PaymentMethod Method = PaymentMethod.Card);
