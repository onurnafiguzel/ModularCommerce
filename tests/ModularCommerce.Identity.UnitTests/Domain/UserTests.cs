using FluentAssertions;
using ModularCommerce.Identity.Domain.Users;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Domain;

public class UserTests
{
    private static Email ValidEmail() => Email.Create("ali@example.com").Value;

    [Fact(DisplayName = "Geçerli değerlerle kullanıcı oluşturulur ve UserRegistered raise edilir")]
    public void Create_WithValidValues_ReturnsUserAndRaisesEvent()
    {
        var email = ValidEmail();

        var result = User.Create(email, "hash-degeri");

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be(email);
        result.Value.PasswordHash.Should().Be("hash-degeri");
        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegistered>()
            .Which.UserId.Should().Be(result.Value.Id);
    }

    [Theory(DisplayName = "Boş şifre özeti reddedilir (Domain hash üretmez ama boşunu da kabul etmez)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPasswordHash_ReturnsFailure(string? passwordHash)
    {
        var result = User.Create(ValidEmail(), passwordHash!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.EmptyPasswordHash);
    }
}
