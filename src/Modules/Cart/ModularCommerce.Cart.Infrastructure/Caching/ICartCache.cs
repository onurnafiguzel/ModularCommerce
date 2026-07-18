using ModularCommerce.Cart.Domain.Carts;

namespace ModularCommerce.Cart.Infrastructure.Caching;

public interface ICartCache
{
    Task<Domain.Carts.Cart?> GetAsync(Guid customerId, CancellationToken cancellationToken);
    Task SetAsync(Domain.Carts.Cart cart, CancellationToken cancellationToken);
    Task RemoveAsync(Guid customerId, CancellationToken cancellationToken);
}
