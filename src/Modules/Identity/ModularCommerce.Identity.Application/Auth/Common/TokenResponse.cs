namespace ModularCommerce.Identity.Application.Auth.Common;

public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
