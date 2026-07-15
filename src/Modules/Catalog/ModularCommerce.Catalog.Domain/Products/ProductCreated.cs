using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Domain.Products;

/// <summary>
/// Ürün oluşturuldu domain olayı. Arama metnini kuran alanları (Name/Description/Sku) taşır ki dışa
/// açılan integration event tüketicisi (Discovery) Catalog'a geri sorgu yapmadan indeksleyebilsin.
/// </summary>
public sealed record ProductCreated(
    Guid ProductId,
    string Name,
    string? Description,
    string Sku,
    bool IsActive,
    DateTime OccurredOnUtc) : IDomainEvent;
