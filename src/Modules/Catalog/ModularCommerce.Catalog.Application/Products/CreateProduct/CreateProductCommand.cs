namespace ModularCommerce.Catalog.Application.Products.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    string Currency,
    int StockQuantity);
