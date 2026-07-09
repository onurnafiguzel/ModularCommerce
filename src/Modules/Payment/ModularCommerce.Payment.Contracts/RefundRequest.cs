namespace ModularCommerce.Payment.Contracts;
public sealed record RefundRequest(
    Guid CustomerId,
    Guid OrderId,
    string IdempotencyKey,
    decimal Amount);
