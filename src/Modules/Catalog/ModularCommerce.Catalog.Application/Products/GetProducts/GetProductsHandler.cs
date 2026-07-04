using FluentValidation;
using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Common;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Application.Products.GetProducts;

public sealed class GetProductsHandler(
    IProductQueries queries,
    IValidator<GetProductsQuery> validator)
{
    public async Task<Result<PagedResponse<ProductSummaryResponse>>> HandleAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PagedResponse<ProductSummaryResponse>>(Error.Validation(
                "Catalog.Products.InvalidQuery",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var page = await queries.GetProductsAsync(query, cancellationToken);
        return Result.Success(page);
    }
}
