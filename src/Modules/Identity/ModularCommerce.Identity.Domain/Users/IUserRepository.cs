using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Domain.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);
    void Add(User user);
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
