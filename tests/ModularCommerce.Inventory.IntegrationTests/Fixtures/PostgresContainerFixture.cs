using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests.Fixtures;

/// <summary>Inventory şeması için Testcontainers Postgres fixture'ı (ortak TestKit tabanı).</summary>
public sealed class PostgresContainerFixture : PostgresFixture<InventoryDbContext>
{
    protected override string Schema => InventoryDbContext.Schema;
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
