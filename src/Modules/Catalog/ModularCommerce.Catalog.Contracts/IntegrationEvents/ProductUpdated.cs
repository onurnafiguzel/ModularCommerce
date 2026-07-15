namespace ModularCommerce.Catalog.Contracts.IntegrationEvents;

/// <summary>Dışa açık "ürün güncellendi" integration event'i — Discovery yeniden indeksler.</summary>
public sealed record ProductUpdated(
    Guid ProductId,
    string Name,
    string? Description,
    string Sku,
    bool IsActive,
    DateTime OccurredOnUtc);
