using FluentAssertions;
using ModularCommerce.Catalog.Infrastructure.Outbox;
using Xunit;
using ContractEvents = ModularCommerce.Catalog.Contracts.IntegrationEvents;
using DomainEvents = ModularCommerce.Catalog.Domain.Products;

namespace ModularCommerce.Catalog.UnitTests.Outbox;

/// <summary>
/// Registry, domain event'i dışa açık integration event'e eşler (OCP genişleme noktası) ve stabil
/// discriminator'ı CLR tipine geri çözer (dispatcher deserialize hedefi).
/// </summary>
public sealed class CatalogIntegrationEventRegistryTests
{
    private readonly CatalogIntegrationEventRegistry _registry = new();

    [Fact(DisplayName = "ProductCreated domain → contract event'e alanlarıyla eşlenir")]
    public void TryMap_ProductCreated_MapsToContract()
    {
        var id = Guid.NewGuid();
        var domainEvent = new DomainEvents.ProductCreated(id, "Kulaklık", "Kablosuz", "ELK-1", true, DateTime.UtcNow);

        var mapped = _registry.TryMap(domainEvent);

        mapped.Should().NotBeNull();
        mapped!.Value.Type.Should().Be("ProductCreated");
        var contract = mapped.Value.IntegrationEvent.Should().BeOfType<ContractEvents.ProductCreated>().Subject;
        contract.ProductId.Should().Be(id);
        contract.Name.Should().Be("Kulaklık");
        contract.Sku.Should().Be("ELK-1");
    }

    [Fact(DisplayName = "ProductUpdated domain → contract event'e eşlenir")]
    public void TryMap_ProductUpdated_MapsToContract()
    {
        var domainEvent = new DomainEvents.ProductUpdated(Guid.NewGuid(), "Ad", null, "SKU-1", false, DateTime.UtcNow);

        var mapped = _registry.TryMap(domainEvent);

        mapped.Should().NotBeNull();
        mapped!.Value.Type.Should().Be("ProductUpdated");
        mapped.Value.IntegrationEvent.Should().BeOfType<ContractEvents.ProductUpdated>();
    }

    [Theory(DisplayName = "Discriminator CLR tipine geri çözülür")]
    [InlineData("ProductCreated", typeof(ContractEvents.ProductCreated))]
    [InlineData("ProductUpdated", typeof(ContractEvents.ProductUpdated))]
    public void ResolveType_KnownDiscriminator_ReturnsClrType(string discriminator, Type expected)
        => _registry.ResolveType(discriminator).Should().Be(expected);

    [Fact(DisplayName = "Bilinmeyen discriminator null döner")]
    public void ResolveType_Unknown_ReturnsNull()
        => _registry.ResolveType("Nope").Should().BeNull();
}
