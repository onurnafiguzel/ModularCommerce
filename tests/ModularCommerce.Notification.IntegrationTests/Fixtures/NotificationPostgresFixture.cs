using ModularCommerce.Notification.Infrastructure.Persistence;
using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Notification.IntegrationTests.Fixtures;

/// <summary>Notification şeması için Testcontainers Postgres fixture'ı (ortak TestKit tabanı).</summary>
public sealed class NotificationPostgresFixture : PostgresFixture<NotificationDbContext>
{
    protected override string Schema => NotificationDbContext.Schema;
}

[CollectionDefinition("NotificationPostgres")]
public sealed class NotificationPostgresCollection : ICollectionFixture<NotificationPostgresFixture>;
