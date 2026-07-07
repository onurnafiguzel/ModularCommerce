using FluentValidation;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Common;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Application.Auth.Refresh;

public sealed class RefreshHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IValidator<RefreshCommand> validator)
{
    public async Task<Result<TokenResponse>> HandleAsync(
        RefreshCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TokenResponse>(Error.Validation(
                "Identity.Refresh.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = tokenService.HashRefreshTokenValue(command.RefreshToken);

        var existingToken = await refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existingToken is null || !existingToken.IsActive(utcNow))
        {
            // Bulunamadı / süresi dolmuş / iptal edilmiş: tek kod (bilgi sızmaz).
            return Result.Failure<TokenResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        var user = await users.GetByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<TokenResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        // Rotasyon (FR-1.3): eski token iptal edilir, yenisi AYNI kayıtta yaratılır —
        // eski değer ikinci kez kullanılamaz (replay penceresi kapanır).
        existingToken.Revoke(utcNow);

        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email.Value);
        var newRefreshToken = tokenService.GenerateRefreshToken();

        var tokenResult = RefreshToken.Create(
            user.Id, newRefreshToken.TokenHash, newRefreshToken.ExpiresAtUtc);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<TokenResponse>(tokenResult.Error);
        }

        refreshTokens.Add(tokenResult.Value);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return Result.Success(new TokenResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            newRefreshToken.Value,
            newRefreshToken.ExpiresAtUtc));
    }
}
