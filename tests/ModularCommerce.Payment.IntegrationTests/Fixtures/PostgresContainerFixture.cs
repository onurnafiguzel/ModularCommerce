using ModularCommerce.Payment.Infrastructure.Persistence;
using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Payment.IntegrationTests.Fixtures;

/// <summary>Payment şeması için Testcontainers Postgres fixture'ı (ortak TestKit tabanı).</summary>
public sealed class PostgresContainerFixture : PostgresFixture<PaymentDbContext>
{
    protected override string Schema => PaymentDbContext.Schema;
}

[CollectionDefinition("PaymentPostgres")]
public sealed class PaymentPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
