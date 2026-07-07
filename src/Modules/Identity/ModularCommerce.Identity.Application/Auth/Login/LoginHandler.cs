using FluentValidation;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Application.Auth.Common;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Application.Auth.Login;

public sealed class LoginHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<LoginCommand> validator)
{
    public async Task<Result<TokenResponse>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TokenResponse>(Error.Validation(
                "Identity.Login.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
        {          
            return Result.Failure<TokenResponse>(IdentityErrors.InvalidCredentials);
        }

        var user = await users.GetByEmailAsync(emailResult.Value, cancellationToken);
        if (user is null)
        {
            passwordHasher.Verify(passwordHasher.DummyHash, command.Password);
            return Result.Failure<TokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            return Result.Failure<TokenResponse>(IdentityErrors.InvalidCredentials);
        }

        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email.Value);
        var refreshToken = tokenService.GenerateRefreshToken();

        var tokenResult = RefreshToken.Create(
            user.Id, refreshToken.TokenHash, refreshToken.ExpiresAtUtc);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<TokenResponse>(tokenResult.Error);
        }

        refreshTokens.Add(tokenResult.Value);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return Result.Success(new TokenResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            refreshToken.ExpiresAtUtc));
    }
}
