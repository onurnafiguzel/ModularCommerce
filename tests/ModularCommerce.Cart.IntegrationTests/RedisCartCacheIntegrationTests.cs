using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using ModularCommerce.Cart.Infrastructure.Caching;
using ModularCommerce.Cart.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Cart.IntegrationTests;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

/// <summary>
/// Gerçek Redis'e karşı cache davranışı: set→get round-trip, 7 günlük TTL ve yazma-kaydırmalı TTL.
/// (Redis artık KAYNAK değil hızlandırıcı; degrade-to-miss davranışı birim testlerde.)
/// </summary>
[Collection("CartRedis")]
public sealed class RedisCartCacheIntegrationTests(RedisContainerFixture fixture)
{
    private RedisCartCache CreateCache()
        => new(
            fixture.Connection,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Cart:TtlDays"] = "7" })
                .Build(),
            NullLogger<RedisCartCache>.Instance);

    private static Cart CartWithItems(Guid customerId, params (Guid ProductId, int Quantity)[] lines)
    {
        var cart = Cart.Create(customerId).Value;
        foreach (var (productId, quantity) in lines)
        {
            cart.AddItem(productId, quantity).IsSuccess.Should().BeTrue();
        }

        return cart;
    }

    [Fact(DisplayName = "Round-trip: cache'e yazılan sepet satırlarıyla aynen okunur")]
    public async Task SetAndGet_RoundTripsCart()
    {
        var cache = CreateCache();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await cache.SetAsync(CartWithItems(customerId, (productId, 3)), CancellationToken.None);
        var loaded = await cache.GetAsync(customerId, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.CustomerId.Should().Be(customerId);
        loaded.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 3);
    }

    [Fact(DisplayName = "Yazım TTL bırakır: 7 güne çok yakın")]
    public async Task Set_SetsSevenDayTtl()
    {
        var customerId = Guid.NewGuid();

        await CreateCache().SetAsync(CartWithItems(customerId, (Guid.NewGuid(), 1)), CancellationToken.None);

        var ttl = await fixture.Connection.GetDatabase().KeyTimeToLiveAsync($"cart:{customerId}");
        ttl.Should().NotBeNull();
        ttl!.Value.Should().BeGreaterThan(TimeSpan.FromDays(6.9)).And.BeLessOrEqualTo(TimeSpan.FromDays(7));
    }

    [Fact(DisplayName = "TTL yazmada kayar: ikinci SET süreyi 7 güne geri çeker")]
    public async Task Set_RenewsTtlOnEveryWrite()
    {
        var cache = CreateCache();
        var customerId = Guid.NewGuid();
        var cart = CartWithItems(customerId, (Guid.NewGuid(), 1));
        var key = $"cart:{customerId}";
        var database = fixture.Connection.GetDatabase();

        await cache.SetAsync(cart, CancellationToken.None);
        (await database.KeyExpireAsync(key, TimeSpan.FromSeconds(100))).Should().BeTrue();

        await cache.SetAsync(cart, CancellationToken.None);

        var ttl = await database.KeyTimeToLiveAsync(key);
        ttl!.Value.Should().BeGreaterThan(TimeSpan.FromDays(6.9), "her yazım TTL'i yeniler");
    }

    [Fact(DisplayName = "RemoveAsync cache girdisini siler")]
    public async Task Remove_DeletesKey()
    {
        var cache = CreateCache();
        var customerId = Guid.NewGuid();
        await cache.SetAsync(CartWithItems(customerId, (Guid.NewGuid(), 1)), CancellationToken.None);

        await cache.RemoveAsync(customerId, CancellationToken.None);

        (await fixture.Connection.GetDatabase().KeyExistsAsync($"cart:{customerId}")).Should().BeFalse();
    }
}
