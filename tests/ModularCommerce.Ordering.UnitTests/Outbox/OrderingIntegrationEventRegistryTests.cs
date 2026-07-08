using FluentAssertions;
using ModularCommerce.Ordering.Infrastructure.Outbox;
using Xunit;
using ContractEvents = ModularCommerce.Ordering.Contracts.IntegrationEvents;
using DomainEvents = ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.UnitTests.Outbox;

/// <summary>
/// Registry, domain event ile dışa açık integration event arasındaki köprüdür (OCP:
/// yeni event = bir satır). Yalnız dışa duyurulacak event'ler eşlenir; gerisi outbox'a
/// yazılmadan (null ile) atlanır.
/// </summary>
public class OrderingIntegrationEventRegistryTests
{
    private readonly OrderingIntegrationEventRegistry _registry = new();

    [Fact(DisplayName = "OrderPaid domain event'i, alanları korunarak integration event'ine eşlenir")]
    public void TryMap_OrderPaid_MapsToIntegrationEvent()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var occurredOn = DateTime.UtcNow;
        var domainEvent = new DomainEvents.OrderPaid(orderId, customerId, 250m, "TRY", occurredOn);

        var mapped = _registry.TryMap(domainEvent);

        mapped.Should().NotBeNull();
        mapped!.Value.Type.Should().Be("OrderPaid");
        mapped.Value.IntegrationEvent.Should().BeOfType<ContractEvents.OrderPaid>()
            .Which.Should().BeEquivalentTo(new ContractEvents.OrderPaid(
                orderId, customerId, 250m, "TRY", occurredOn));
    }

    [Fact(DisplayName = "Mapping'i olmayan domain event (OrderStatusChanged) null döner — outbox'a yazılmaz")]
    public void TryMap_UnmappedEvent_ReturnsNull()
    {
        var unmapped = new DomainEvents.OrderStatusChanged(
            Guid.NewGuid(), DomainEvents.OrderStatus.Created, DomainEvents.OrderStatus.StockReserved, DateTime.UtcNow);

        _registry.TryMap(unmapped).Should().BeNull();
    }

    [Fact(DisplayName = "ResolveType discriminator'ı integration event CLR tipine çevirir (deserialize hedefi)")]
    public void ResolveType_KnownDiscriminator_ReturnsClrType()
    {
        _registry.ResolveType("OrderPaid").Should().Be<ContractEvents.OrderPaid>();
    }

    [Fact(DisplayName = "ResolveType bilinmeyen discriminator için null döner")]
    public void ResolveType_UnknownDiscriminator_ReturnsNull()
    {
        _registry.ResolveType("BilinmeyenTip").Should().BeNull();
    }
}
