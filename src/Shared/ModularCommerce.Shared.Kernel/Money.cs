namespace ModularCommerce.Shared.Kernel;

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

    public Money Add(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"Farklı para birimleri toplanamaz: {Currency} + {other.Currency}.");
        }

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity, "Adet negatif olamaz.");
        }

        return new Money(Amount * quantity, Currency);
    }
}

public static class MoneyErrors
{
    public static readonly Error NegativeAmount = Error.Validation(
        "Money.NegativeAmount",
        "Tutar negatif olamaz.");

    public static readonly Error InvalidCurrency = Error.Validation(
        "Money.InvalidCurrency",
        "Para birimi 3 harfli ISO kodu olmalıdır (örn. TRY).");
}
