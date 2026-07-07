using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Contracts;

public interface ICartService
{
    Task<Result<IReadOnlyList<CartLineDto>>> GetItemsAsync(
        Guid customerId,
        CancellationToken cancellationToken);
    Task<Result> ClearAsync(Guid customerId, CancellationToken cancellationToken);
}
