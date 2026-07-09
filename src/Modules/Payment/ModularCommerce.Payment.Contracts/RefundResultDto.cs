namespace ModularCommerce.Payment.Contracts;
public sealed record RefundResultDto(
    Guid PaymentId,
    string? RefundTransactionId,
    DateTime? RefundedAtUtc);
