using Microsoft.AspNetCore.Identity;
using ModularCommerce.Identity.Application.Abstractions;
using ModularCommerce.Identity.Domain.Users;

namespace ModularCommerce.Identity.Infrastructure.Security;
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private const int PasswordHashIterations = 20_000;

    private readonly PasswordHasher<User> _hasher = new(
        Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
        {
            IterationCount = PasswordHashIterations,
        }));

    public IdentityPasswordHasher()
        // Timing-attack önlemi (NFR-1.1 notu): kullanıcı bulunamadığında da
        // doğrulanacak sahte hash bir kez burada üretilir.
        => DummyHash = _hasher.HashPassword(null!, Guid.NewGuid().ToString("N"));

    public string DummyHash { get; }

    public string Hash(string password)
        => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string providedPassword)
        => _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
