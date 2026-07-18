using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Cart.Infrastructure.Caching;
using ModularCommerce.Cart.Infrastructure.Persistence;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Caching;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

/// <summary>
/// RedisCartCache artık KAYNAK değil, hızlandırıcı → graceful degrade: Redis bağlantı/timeout hatasında
/// Get null (miss) döner, Set/Remove sessizce geçer (ASLA fırlatmaz) → çağıran Postgres'e düşer.
/// Değer varsa JSON doğru Cart'a çözülür.
/// </summary>
public sealed class RedisCartCacheTests
{
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly RedisCartCache _sut;

    public RedisCartCacheTests()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(_db);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cart:TtlDays"] = "7" })
            .Build();

        _sut = new RedisCartCache(redis, configuration, NullLogger<RedisCartCache>.Instance);
    }

    private static readonly RedisConnectionException Down =
        new(ConnectionFailureType.UnableToConnect, "redis down");

    [Fact(DisplayName = "Redis erişilemezse GetAsync null döner (miss → Postgres'e düşülür)")]
    public async Task GetAsync_WhenConnectionFails_ReturnsNull()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>()).ThrowsAsyncForAnyArgs(Down);

        var result = await _sut.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "Değer varsa GetAsync JSON'ı Cart'a çözer")]
    public async Task GetAsync_WhenValuePresent_ReturnsCart()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(productId, 2);
        var json = JsonSerializer.Serialize(CartDocument.FromCart(cart), JsonSerializerOptions.Web);
        _db.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)json);

        var result = await _sut.GetAsync(customerId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(customerId);
        result.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 2);
    }

    [Fact(DisplayName = "Redis erişilemezse SetAsync fırlatmaz (best-effort)")]
    public async Task SetAsync_WhenConnectionFails_DoesNotThrow()
    {
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>())
            .ThrowsAsyncForAnyArgs(Down);
        var cart = Cart.Create(Guid.NewGuid()).Value;
        cart.AddItem(Guid.NewGuid(), 1);

        var act = async () => await _sut.SetAsync(cart, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
