using ModularCommerce.Catalog.Application.Common;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Application.Products.GetProducts;

namespace ModularCommerce.Catalog.Application.Abstractions;

public interface IProductQueries
{
    Task<PagedResponse<ProductSummaryResponse>> GetProductsAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken);
}
