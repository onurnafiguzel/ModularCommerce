using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Shared.Infrastructure.Auth;

namespace ModularCommerce.Identity.Infrastructure.Security;
public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessTokenResult GenerateAccessToken(Guid userId, string email)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAtUtc,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Email] = email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });

        return new AccessTokenResult(token, expiresAtUtc);
    }

    public RefreshTokenResult GenerateRefreshToken()
    {
        // 64 bayt kriptografik rastgelelik: ham değer YALNIZ istemciye döner,
        // DB'ye SHA-256 özeti yazılır (sızıntıda kullanılamaz).
        var rawValue = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

        return new RefreshTokenResult(
            rawValue,
            HashRefreshTokenValue(rawValue),
            DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshTokenValue(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
