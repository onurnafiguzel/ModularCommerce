using FluentValidation;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Application.Auth.Signup;

public sealed class SignupHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IValidator<SignupCommand> validator)
{
    public async Task<Result<SignupResponse>> HandleAsync(
        SignupCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SignupResponse>(Error.Validation(
                "Identity.Signup.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<SignupResponse>(emailResult.Error);
        }
        
        var existing = await users.GetByEmailAsync(emailResult.Value, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<SignupResponse>(IdentityErrors.EmailAlreadyExists);
        }

        var userResult = User.Create(emailResult.Value, passwordHasher.Hash(command.Password));
        if (userResult.IsFailure)
        {
            return Result.Failure<SignupResponse>(userResult.Error);
        }

        users.Add(userResult.Value);

        var saveResult = await users.SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result.Failure<SignupResponse>(saveResult.Error);
        }

        return Result.Success(new SignupResponse(userResult.Value.Id, emailResult.Value.Value));
    }
}
