using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Infrastructure.Outbox;

/// <summary>
/// Domain event ile dışa açık integration event arasındaki köprü (Adapter + Registry). Dar arayüz
/// (ISP): interceptor yalnız ileri eşlemeyi, dispatcher yalnız ters çözümlemeyi kullanır. Yeni event
/// yayınlamak bu arayüzün implementasyonuna BİR satır eklemektir (OCP). (Ordering-lokal ikizi cross-module
/// referans edilemediğinden Catalog kendi kopyasını taşır.)
/// </summary>
public interface IIntegrationEventMapper
{
    /// <summary>
    /// Domain event → (stabil discriminator, integration event nesnesi). Mapping yoksa null:
    /// yalnız dışa duyurulacak event'ler outbox'a yazılır.
    /// </summary>
    (string Type, object IntegrationEvent)? TryMap(IDomainEvent domainEvent);

    /// <summary>Dispatcher için: stabil discriminator → CLR tipi (deserialize hedefi).</summary>
    Type? ResolveType(string type);
}
