namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>
/// Domain'in kendi yöntem temsili — Contracts'taki enum ile aynı değerler, ama Domain
/// yalnız Shared.Kernel'e referans verebildiği için (mimari kural) burada tekrarlanır;
/// eşlemeyi contract adapter yapar.
/// </summary>
public enum PaymentMethod
{
    Card = 0,
    Wallet = 1,
    BankTransfer = 2,
}
