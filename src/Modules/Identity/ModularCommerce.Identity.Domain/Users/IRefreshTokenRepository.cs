namespace ModularCommerce.Identity.Domain.Users;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(RefreshToken refreshToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
