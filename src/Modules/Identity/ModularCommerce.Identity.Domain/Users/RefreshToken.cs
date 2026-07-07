using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Domain.Users;

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Result<RefreshToken> Create(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<RefreshToken>(IdentityErrors.InvalidUserId);
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Result.Failure<RefreshToken>(IdentityErrors.EmptyTokenHash);
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            return Result.Failure<RefreshToken>(IdentityErrors.InvalidExpiry);
        }

        return Result.Success(new RefreshToken(userId, tokenHash, expiresAtUtc));
    }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;

    /// <summary>İdempotent: zaten iptal edilmiş token'da ilk iptal zamanı korunur.</summary>
    public Result Revoke(DateTime utcNow)
    {
        if (RevokedAtUtc is null)
        {
            RevokedAtUtc = utcNow;
        }

        return Result.Success();
    }
}
