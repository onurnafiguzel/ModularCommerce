using ModularCommerce.Identity.Infrastructure.Persistence;
using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Identity.IntegrationTests.Fixtures;

/// <summary>Identity şeması için Testcontainers Postgres fixture'ı (ortak TestKit tabanı).</summary>
public sealed class PostgresContainerFixture : PostgresFixture<IdentityDbContext>
{
    protected override string Schema => IdentityDbContext.Schema;
}

[CollectionDefinition("IdentityPostgres")]
public sealed class IdentityPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
