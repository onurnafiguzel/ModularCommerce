namespace ModularCommerce.Catalog.Contracts.IntegrationEvents;

/// <summary>
/// Dışa açık "ürün oluşturuldu" integration event'i (domain event'in ayrı POCO ikizi). Arama metnini
/// kuran alanları taşır; tüketici (Discovery) bunlardan embedding üretir.
/// </summary>
public sealed record ProductCreated(
    Guid ProductId,
    string Name,
    string? Description,
    string Sku,
    bool IsActive,
    DateTime OccurredOnUtc);
