namespace ModularCommerce.Catalog.Contracts;
public sealed record ProductSnapshotDto(
    Guid ProductId,
    string Name,
    decimal Price,
    string Currency,
    bool IsActive);
