using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Domain.Users;

public sealed class User : Entity
{
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private User()
    {
    }

    private User(Email email, string passwordHash)
    {
        Email = email;
        PasswordHash = passwordHash;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Result<User> Create(Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(IdentityErrors.EmptyPasswordHash);
        }

        var user = new User(email, passwordHash);
        user.Raise(new UserRegistered(user.Id, email.Value, user.CreatedAtUtc));

        return Result.Success(user);
    }
}
