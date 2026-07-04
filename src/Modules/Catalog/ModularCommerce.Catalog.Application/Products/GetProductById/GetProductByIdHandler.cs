using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Application.Products.GetProductById;

public sealed class GetProductByIdHandler(IProductRepository repository)
{
    public async Task<Result<ProductDetailResponse>> HandleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductDetailResponse>(ProductErrors.NotFound(id));
        }

        return Result.Success(new ProductDetailResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Sku,
            product.Price.Amount,
            product.Price.Currency,
            product.StockQuantity,
            product.IsActive,
            product.CreatedAtUtc));
    }
}
