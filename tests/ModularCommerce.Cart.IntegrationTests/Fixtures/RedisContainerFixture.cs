using ModularCommerce.TestKit;
using Xunit;

namespace ModularCommerce.Cart.IntegrationTests.Fixtures;

/// <summary>Redis test container'ı (ortak TestKit tabanı).</summary>
public sealed class RedisContainerFixture : RedisFixture;

[CollectionDefinition("CartRedis")]
public sealed class CartRedisCollection : ICollectionFixture<RedisContainerFixture>;
