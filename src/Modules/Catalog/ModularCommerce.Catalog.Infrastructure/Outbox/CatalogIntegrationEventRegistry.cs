using ModularCommerce.Shared.Kernel;
using ContractEvents = ModularCommerce.Catalog.Contracts.IntegrationEvents;
using DomainEvents = ModularCommerce.Catalog.Domain.Products;

namespace ModularCommerce.Catalog.Infrastructure.Outbox;

/// <summary>
/// Catalog'un domain→integration event kayıt defteri. TEK genişleme noktası (OCP): yeni bir olgu
/// yayınlamak = buraya bir girdi; interceptor ve dispatcher değişmez. Stabil discriminator storage'ı
/// CLR tipinden ayırır (rename/taşımada kırılmaz).
/// </summary>
public sealed class CatalogIntegrationEventRegistry : IIntegrationEventMapper
{
    private const string ProductCreatedType = "ProductCreated";
    private const string ProductUpdatedType = "ProductUpdated";

    // İleri eşleme: domain event tipi → (discriminator, integration event factory).
    private readonly Dictionary<Type, (string Type, Func<IDomainEvent, object> Factory)> _map = new()
    {
        [typeof(DomainEvents.ProductCreated)] = (ProductCreatedType, e =>
        {
            var d = (DomainEvents.ProductCreated)e;
            return new ContractEvents.ProductCreated(d.ProductId, d.Name, d.Description, d.Sku, d.IsActive, d.OccurredOnUtc);
        }),
        [typeof(DomainEvents.ProductUpdated)] = (ProductUpdatedType, e =>
        {
            var d = (DomainEvents.ProductUpdated)e;
            return new ContractEvents.ProductUpdated(d.ProductId, d.Name, d.Description, d.Sku, d.IsActive, d.OccurredOnUtc);
        }),
    };

    // Ters eşleme: discriminator → integration event CLR tipi (dispatcher deserialize eder).
    private readonly Dictionary<string, Type> _types = new()
    {
        [ProductCreatedType] = typeof(ContractEvents.ProductCreated),
        [ProductUpdatedType] = typeof(ContractEvents.ProductUpdated),
    };

    public (string Type, object IntegrationEvent)? TryMap(IDomainEvent domainEvent)
        => _map.TryGetValue(domainEvent.GetType(), out var mapping)
            ? (mapping.Type, mapping.Factory(domainEvent))
            : null;

    public Type? ResolveType(string type)
        => _types.TryGetValue(type, out var clrType) ? clrType : null;
}
