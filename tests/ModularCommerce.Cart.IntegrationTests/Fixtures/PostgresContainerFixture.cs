using ModularCommerce.Cart.Infrastructure.Persistence;
using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Cart.IntegrationTests.Fixtures;

/// <summary>Cart şeması için Testcontainers Postgres fixture'ı (ortak TestKit tabanı) — sepet artık kalıcı.</summary>
public sealed class PostgresContainerFixture : PostgresFixture<CartDbContext>
{
    protected override string Schema => CartDbContext.Schema;
}

[CollectionDefinition("CartPostgres")]
public sealed class CartPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
