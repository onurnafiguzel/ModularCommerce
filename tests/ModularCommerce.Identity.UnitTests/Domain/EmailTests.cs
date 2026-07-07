using FluentAssertions;
using ModularCommerce.Identity.Domain.Users;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Domain;

public class EmailTests
{
    [Fact(DisplayName = "E-posta trim + küçük harfe normalize edilir (FR-1.5 unique kontrol tek biçimle çalışır)")]
    public void Create_NormalizesTrimAndCase()
    {
        var result = Email.Create("  Ali.Veli@Example.COM  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("ali.veli@example.com");
    }

    [Theory(DisplayName = "Geçersiz biçimler reddedilir")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ali")]
    [InlineData("ali@")]
    [InlineData("@example.com")]
    [InlineData("ali@example")]
    [InlineData("ali veli@example.com")]
    [InlineData("ali@exa mple.com")]
    public void Create_WithInvalidFormat_ReturnsFailure(string? value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidEmail);
    }

    [Fact(DisplayName = "254 karakteri aşan e-posta reddedilir")]
    public void Create_ExceedingMaxLength_ReturnsFailure()
    {
        var local = new string('a', Email.MaxLength);
        var result = Email.Create($"{local}@example.com");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidEmail);
    }

    [Fact(DisplayName = "Aynı adresin farklı yazımları eşittir (value object semantiği)")]
    public void Emails_WithSameNormalizedValue_AreEqual()
    {
        var first = Email.Create("Ali@Example.com").Value;
        var second = Email.Create("ali@example.com ").Value;

        first.Should().Be(second);
    }
}
