namespace ModularCommerce.Catalog.Application.Products.Common;

public sealed record ProductSummaryResponse(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    int StockQuantity);
