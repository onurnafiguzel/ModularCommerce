namespace ModularCommerce.Catalog.Application.Products.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null);
