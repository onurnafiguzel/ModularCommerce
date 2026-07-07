using FluentValidation;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Application.Auth.Logout;

public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IValidator<LogoutCommand> validator)
{
    public async Task<Result> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(Error.Validation(
                "Identity.Logout.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var tokenHash = tokenService.HashRefreshTokenValue(command.RefreshToken);

        var token = await refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (token is null || token.UserId != command.UserId)
        {
            return Result.Success();
        }

        token.Revoke(DateTime.UtcNow);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
