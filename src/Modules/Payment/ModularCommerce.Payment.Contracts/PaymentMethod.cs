namespace ModularCommerce.Payment.Contracts;

/// <summary>Ödeme yöntemi (FR-6.5 Strategy); bu hafta yalnız Card implementasyonu var.</summary>
public enum PaymentMethod
{
    Card = 0,
    Wallet = 1,
    BankTransfer = 2,
}
