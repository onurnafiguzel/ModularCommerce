using ModularCommerce.Shared.Kernel;
using ContractEvents = ModularCommerce.Ordering.Contracts.IntegrationEvents;
using DomainEvents = ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.Infrastructure.Outbox;

/// <summary>
/// Ordering'in domain→integration event kayıt defteri. TEK genişleme noktası (OCP):
/// yeni bir olgu yayınlamak = buraya bir Register satırı; interceptor ve dispatcher
/// hiç değişmez. W9'da OrderCancelled ikinci girdiyle bu genişlemeyi kanıtlar. Stabil
/// discriminator storage'ı CLR tipinden ayırır (rename/taşımada kırılmaz — D5).
/// </summary>
public sealed class OrderingIntegrationEventRegistry : IIntegrationEventMapper
{
    private const string OrderPaidType = "OrderPaid";
    private const string OrderCancelledType = "OrderCancelled";

    // İleri eşleme: domain event tipi → (discriminator, integration event factory).
    private readonly Dictionary<Type, (string Type, Func<IDomainEvent, object> Factory)> _map = new()
    {
        [typeof(DomainEvents.OrderPaid)] = (OrderPaidType, e =>
        {
            var d = (DomainEvents.OrderPaid)e;
            return new ContractEvents.OrderPaid(d.OrderId, d.CustomerId, d.TotalAmount, d.Currency, d.OccurredOnUtc);
        }),
        [typeof(DomainEvents.OrderCancelled)] = (OrderCancelledType, e =>
        {
            var d = (DomainEvents.OrderCancelled)e;
            return new ContractEvents.OrderCancelled(d.OrderId, d.CustomerId, d.OccurredOnUtc);
        }),
    };

    // Ters eşleme: discriminator → integration event CLR tipi (dispatcher deserialize eder).
    private readonly Dictionary<string, Type> _types = new()
    {
        [OrderPaidType] = typeof(ContractEvents.OrderPaid),
        [OrderCancelledType] = typeof(ContractEvents.OrderCancelled),
    };

    public (string Type, object IntegrationEvent)? TryMap(IDomainEvent domainEvent)
        => _map.TryGetValue(domainEvent.GetType(), out var mapping)
            ? (mapping.Type, mapping.Factory(domainEvent))
            : null;

    public Type? ResolveType(string type)
        => _types.TryGetValue(type, out var clrType) ? clrType : null;
}
