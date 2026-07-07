using Microsoft.EntityFrameworkCore;
using ModularCommerce.Identity.Domain.Users;

namespace ModularCommerce.Identity.Infrastructure.Persistence.Repositories;
public sealed class RefreshTokenRepository(IdentityDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    public void Add(RefreshToken refreshToken)
        => context.RefreshTokens.Add(refreshToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);
}
