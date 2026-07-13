using FluentAssertions;
using ModularCommerce.Shared.Kernel;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Domain;

/// <summary>Money value object'inin iş kuralları — para kuralının tek test yüzeyi.</summary>
public class MoneyTests
{
    [Fact(DisplayName = "Geçerli tutar ve para birimi ile Money oluşturulur")]
    public void Create_WithValidValues_ReturnsSuccess()
    {
        var result = Money.Create(99.90m, "TRY");

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(99.90m);
        result.Value.Currency.Should().Be("TRY");
    }

    [Fact(DisplayName = "Para birimi verilmezse TRY varsayılır")]
    public void Create_WithoutCurrency_DefaultsToTry()
    {
        var result = Money.Create(10m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be(Money.DefaultCurrency);
    }

    [Fact(DisplayName = "Negatif tutar reddedilir")]
    public void Create_WithNegativeAmount_ReturnsFailure()
    {
        var result = Money.Create(-1m);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MoneyErrors.NegativeAmount);
    }

    [Theory(DisplayName = "Geçersiz para birimi kodu reddedilir")]
    [InlineData("TL")]
    [InlineData("TRYX")]
    [InlineData("try")]
    [InlineData("T1Y")]
    [InlineData("")]
    public void Create_WithInvalidCurrency_ReturnsFailure(string currency)
    {
        var result = Money.Create(10m, currency);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MoneyErrors.InvalidCurrency);
    }

    [Fact(DisplayName = "Aynı tutar ve para birimi değer eşitliği taşır")]
    public void Money_WithSameValues_AreEqual()
    {
        var first = Money.Create(10m, "TRY").Value;
        var second = Money.Create(10m, "TRY").Value;

        first.Should().Be(second);
    }
}
