using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Infrastructure.Outbox;
using ModularCommerce.Ordering.Infrastructure.Persistence;
using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests.Fixtures;

/// <summary>Ordering şeması için Testcontainers Postgres fixture'ı (ortak TestKit tabanı).</summary>
public sealed class PostgresContainerFixture : PostgresFixture<OrderingDbContext>
{
    protected override string Schema => OrderingDbContext.Schema;

    /// <summary>
    /// Outbox interceptor'ı bağlı context — üretimdeki OrderingModule kaydını taklit eder.
    /// SaveChanges anında domain event'ler outbox satırına çevrilir (atomiklik testleri için).
    /// </summary>
    public OrderingDbContext CreateContextWithOutbox()
        => Instantiate(BuildOptions(builder => builder.AddInterceptors(
            new DomainEventToOutboxInterceptor(new OrderingIntegrationEventRegistry()))));
}

[CollectionDefinition("OrderingPostgres")]
public sealed class OrderingPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
