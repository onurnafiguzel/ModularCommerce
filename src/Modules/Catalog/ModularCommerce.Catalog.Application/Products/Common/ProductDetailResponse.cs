namespace ModularCommerce.Catalog.Application.Products.Common;

public sealed record ProductDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    string Currency,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAtUtc);
