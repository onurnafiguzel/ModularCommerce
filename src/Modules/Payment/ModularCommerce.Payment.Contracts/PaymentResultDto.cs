namespace ModularCommerce.Payment.Contracts;

/// <summary>Başarılı (Completed) ödemenin sözleşme görünümü; replay'de aynen döner.</summary>
public sealed record PaymentResultDto(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string? PspTransactionId,
    DateTime? CompletedAtUtc);
