using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Domain.ValueObjects;

public sealed record Money
{
    public const string DefaultCurrency = "TRY";

    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);
        }

        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure<Money>(MoneyErrors.InvalidCurrency);
        }

        return Result.Success(new Money(amount, currency));
    }
}

/// <summary>Money value object'inin hata katalogu.</summary>
public static class MoneyErrors
{
    public static readonly Error NegativeAmount = Error.Validation(
        "Catalog.Money.NegativeAmount",
        "Tutar negatif olamaz.");

    public static readonly Error InvalidCurrency = Error.Validation(
        "Catalog.Money.InvalidCurrency",
        "Para birimi 3 harfli ISO kodu olmalıdır (örn. TRY).");
}
