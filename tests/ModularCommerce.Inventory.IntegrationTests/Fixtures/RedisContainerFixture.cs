using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests.Fixtures;

/// <summary>Redis test container'ı (ortak TestKit tabanı).</summary>
public sealed class RedisContainerFixture : RedisFixture;

/// <summary>
/// Postgres + Redis birlikte gereken testler için kompozit koleksiyon —
/// iki fixture aynen yeniden kullanılır (bootstrap kopyalanmaz).
/// </summary>
[CollectionDefinition("PostgresRedis")]
public sealed class PostgresRedisCollection :
    ICollectionFixture<PostgresContainerFixture>,
    ICollectionFixture<RedisContainerFixture>;
