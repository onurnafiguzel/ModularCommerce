using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Application.Products.GetProductById;

public sealed class GetProductByIdHandler(IProductQueries queries)
{
    public async Task<Result<ProductDetailResponse>> HandleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Okuma-modeli üzerinden (cache-decorate edilebilir); yazma-modeli repository'sine bağlanmaz.
        var product = await queries.GetProductByIdAsync(id, cancellationToken);

        return product is null
            ? Result.Failure<ProductDetailResponse>(ProductErrors.NotFound(id))
            : Result.Success(product);
    }
}
