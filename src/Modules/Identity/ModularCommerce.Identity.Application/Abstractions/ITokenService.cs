namespace ModularCommerce.Identity.Application.Abstractions;

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(Guid userId, string email);
    RefreshTokenResult GenerateRefreshToken();
    string HashRefreshTokenValue(string value);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

public sealed record RefreshTokenResult(string Value, string TokenHash, DateTime ExpiresAtUtc);
