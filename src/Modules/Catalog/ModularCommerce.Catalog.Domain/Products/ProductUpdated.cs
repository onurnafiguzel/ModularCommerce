using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Domain.Products;

/// <summary>
/// Ürün güncellendi domain olayı. Değişen arama alanlarını taşır → Discovery yeniden embed eder.
/// </summary>
public sealed record ProductUpdated(
    Guid ProductId,
    string Name,
    string? Description,
    string Sku,
    bool IsActive,
    DateTime OccurredOnUtc) : IDomainEvent;
