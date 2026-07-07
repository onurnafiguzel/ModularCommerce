using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModularCommerce.Identity.Infrastructure.Security;
using ModularCommerce.Shared.Infrastructure.Auth;
using Xunit;

namespace ModularCommerce.Identity.UnitTests.Infrastructure;

/// <summary>
/// WebApplicationFactory'siz JWT kanıtı: üretilen token, Host'un JwtBearer'da
/// kullandığı AYNI parametrelerle (JsonWebTokenHandler + TokenValidationParameters)
/// doğrulanır — NFR-1.2'nin (lokal doğrulama) birim seviyesi karşılığı.
/// </summary>
public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "ModularCommerce.Test",
        Audience = "ModularCommerce.Test",
        SigningKey = "unit-test-signing-key-en-az-32-karakter!",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    };

    private readonly JwtTokenService _service = new(Microsoft.Extensions.Options.Options.Create(Options));

    [Fact(DisplayName = "Üretilen access token aynı parametrelerle lokal doğrulanır; sub/email/iss/aud doğru (NFR-1.2)")]
    public async Task GenerateAccessToken_ProducesLocallyValidatableToken()
    {
        var userId = Guid.NewGuid();

        var accessToken = _service.GenerateAccessToken(userId, "ali@example.com");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            accessToken.Token,
            new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(Options.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            });

        result.IsValid.Should().BeTrue();
        result.Claims[JwtRegisteredClaimNames.Sub].Should().Be(userId.ToString());
        result.Claims[JwtRegisteredClaimNames.Email].Should().Be("ali@example.com");
        result.Claims.Should().ContainKey(JwtRegisteredClaimNames.Jti);
        accessToken.ExpiresAtUtc.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(Options.AccessTokenMinutes), TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Farklı anahtarla imzalanmış token reddedilir")]
    public async Task Token_SignedWithDifferentKey_FailsValidation()
    {
        var accessToken = _service.GenerateAccessToken(Guid.NewGuid(), "ali@example.com");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            accessToken.Token,
            new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("BAMBASKA-bir-anahtar-en-az-32-karakter!!")),
            });

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Refresh token: ham değer ≠ hash; hash deterministik SHA-256'dır; süre RefreshTokenDays")]
    public void GenerateRefreshToken_ReturnsRawValueAndDeterministicHash()
    {
        var refreshToken = _service.GenerateRefreshToken();

        refreshToken.Value.Should().NotBeNullOrWhiteSpace();
        refreshToken.TokenHash.Should().NotBe(refreshToken.Value, "DB'ye ham değer değil özet yazılır");
        _service.HashRefreshTokenValue(refreshToken.Value).Should().Be(refreshToken.TokenHash);
        refreshToken.ExpiresAtUtc.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(Options.RefreshTokenDays), TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "İki refresh token asla aynı değeri üretmez")]
    public void GenerateRefreshToken_ProducesUniqueValues()
    {
        var first = _service.GenerateRefreshToken();
        var second = _service.GenerateRefreshToken();

        first.Value.Should().NotBe(second.Value);
    }
}
