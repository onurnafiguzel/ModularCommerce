using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Cart.Infrastructure.Caching;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Infrastructure.Persistence;

public sealed class CachingCartRepository(ICartRepository inner, ICartCache cache) : ICartRepository
{
    public async Task<Result<Domain.Carts.Cart?>> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(customerId, cancellationToken);
        if (cached is not null)
        {
            return Result.Success<Domain.Carts.Cart?>(cached);
        }

        var fromSource = await inner.GetAsync(customerId, cancellationToken);
        if (fromSource.IsSuccess && fromSource.Value is not null)
        {
            await cache.SetAsync(fromSource.Value, cancellationToken); // cache'i doldur (best-effort)
        }

        return fromSource;
    }

    public async Task<Result> SaveAsync(Domain.Carts.Cart cart, CancellationToken cancellationToken)
    {
        var saved = await inner.SaveAsync(cart, cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await cache.SetAsync(cart, cancellationToken); // türetilmiş kopyayı güncelle (best-effort)
        return Result.Success();
    }

    public async Task<Result> RemoveAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var removed = await inner.RemoveAsync(customerId, cancellationToken);
        if (removed.IsFailure)
        {
            return removed;
        }

        await cache.RemoveAsync(customerId, cancellationToken); // best-effort
        return Result.Success();
    }
}
