using Microsoft.EntityFrameworkCore;
using ModularCommerce.Identity.Domain.Users;
using ModularCommerce.Shared.Kernel;
using Npgsql;

namespace ModularCommerce.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public void Add(User user)
        => context.Users.Add(user);

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Check-then-insert yarışının kapandığı yer: iki eşzamanlı kayıt
            // ön kontrolü geçse de unique index ikinciyi 23505 ile durdurur (FR-1.5).
            return Result.Failure(IdentityErrors.EmailAlreadyExists);
        }
    }
}
