using FluentAssertions;
using ModularCommerce.Identity.Domain.Users;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Domain;

public class RefreshTokenTests
{
    private static RefreshToken CreateToken(TimeSpan? lifetime = null)
        => RefreshToken.Create(
            Guid.NewGuid(),
            "token-hash",
            DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(7))).Value;

    [Fact(DisplayName = "Geçerli değerlerle token oluşturulur ve aktiftir")]
    public void Create_WithValidValues_ReturnsActiveToken()
    {
        var token = CreateToken();

        token.IsActive(DateTime.UtcNow).Should().BeTrue();
        token.RevokedAtUtc.Should().BeNull();
    }

    [Fact(DisplayName = "Boş kullanıcı kimliği reddedilir")]
    public void Create_WithEmptyUserId_ReturnsFailure()
    {
        var result = RefreshToken.Create(Guid.Empty, "hash", DateTime.UtcNow.AddDays(7));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidUserId);
    }

    [Theory(DisplayName = "Boş token özeti reddedilir")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTokenHash_ReturnsFailure(string? tokenHash)
    {
        var result = RefreshToken.Create(Guid.NewGuid(), tokenHash!, DateTime.UtcNow.AddDays(7));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.EmptyTokenHash);
    }

    [Fact(DisplayName = "Geçmiş son kullanma zamanı reddedilir")]
    public void Create_WithPastExpiry_ReturnsFailure()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddMinutes(-1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidExpiry);
    }

    [Fact(DisplayName = "Süresi dolan token aktif değildir")]
    public void IsActive_AfterExpiry_ReturnsFalse()
    {
        var token = CreateToken(TimeSpan.FromMinutes(5));

        token.IsActive(DateTime.UtcNow.AddMinutes(6)).Should().BeFalse();
    }

    [Fact(DisplayName = "İptal edilen token aktif değildir")]
    public void IsActive_AfterRevoke_ReturnsFalse()
    {
        var token = CreateToken();

        token.Revoke(DateTime.UtcNow);

        token.IsActive(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact(DisplayName = "Revoke idempotenttir: ikinci çağrı ilk iptal zamanını korur")]
    public void Revoke_CalledTwice_KeepsFirstRevocationTime()
    {
        var token = CreateToken();
        var firstRevokeAt = DateTime.UtcNow;

        token.Revoke(firstRevokeAt).IsSuccess.Should().BeTrue();
        token.Revoke(firstRevokeAt.AddMinutes(10)).IsSuccess.Should().BeTrue();

        token.RevokedAtUtc.Should().Be(firstRevokeAt);
    }
}
