using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ModularCommerce.Cart.Infrastructure.Persistence;
using ModularCommerce.Cart.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Cart.IntegrationTests;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

/// <summary>
/// Gerçek Redis'e karşı depo davranışı: round-trip, 7 günlük TTL (FR-4.2),
/// yazma-kaydırmalı TTL ve boşalan sepette anahtar silme.
/// </summary>
[Collection("CartRedis")]
public sealed class RedisCartRepositoryTests(RedisContainerFixture fixture)
{
    private RedisCartRepository CreateRepository()
        => new(
            fixture.Connection,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Cart:TtlDays"] = "7" })
                .Build());

    private static Cart CartWithItems(Guid customerId, params (Guid ProductId, int Quantity)[] lines)
    {
        var cart = Cart.Create(customerId).Value;
        foreach (var (productId, quantity) in lines)
        {
            cart.AddItem(productId, quantity).IsSuccess.Should().BeTrue();
        }

        return cart;
    }

    [Fact(DisplayName = "Round-trip: kaydedilen sepet satırlarıyla aynen geri okunur")]
    public async Task SaveAndGet_RoundTripsCart()
    {
        var repository = CreateRepository();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = CartWithItems(customerId, (productId, 3));

        (await repository.SaveAsync(cart, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var loaded = await repository.GetAsync(customerId, CancellationToken.None);

        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.CustomerId.Should().Be(customerId);
        loaded.Value.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 3);
    }

    [Fact(DisplayName = "Hiç kaydedilmemiş sepet: başarılı ve null (boş sepet demektir)")]
    public async Task Get_UnknownCustomer_ReturnsSuccessNull()
    {
        var loaded = await CreateRepository().GetAsync(Guid.NewGuid(), CancellationToken.None);

        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Should().BeNull();
    }

    [Fact(DisplayName = "Yazım TTL bırakır: 7 güne çok yakın (FR-4.2)")]
    public async Task Save_SetsSevenDayTtl()
    {
        var repository = CreateRepository();
        var customerId = Guid.NewGuid();

        await repository.SaveAsync(CartWithItems(customerId, (Guid.NewGuid(), 1)), CancellationToken.None);

        var ttl = await fixture.Connection.GetDatabase().KeyTimeToLiveAsync($"cart:{customerId}");
        ttl.Should().NotBeNull();
        ttl!.Value.Should().BeGreaterThan(TimeSpan.FromDays(6.9)).And.BeLessOrEqualTo(TimeSpan.FromDays(7));
    }

    [Fact(DisplayName = "TTL yazmada kayar: ikinci SET süreyi 7 güne geri çeker")]
    public async Task Save_RenewsTtlOnEveryWrite()
    {
        var repository = CreateRepository();
        var customerId = Guid.NewGuid();
        var cart = CartWithItems(customerId, (Guid.NewGuid(), 1));
        var key = $"cart:{customerId}";
        var database = fixture.Connection.GetDatabase();

        await repository.SaveAsync(cart, CancellationToken.None);
        // Terk edilmiş sepeti simüle et: TTL'i elle 100 saniyeye indir.
        (await database.KeyExpireAsync(key, TimeSpan.FromSeconds(100))).Should().BeTrue();

        await repository.SaveAsync(cart, CancellationToken.None);

        var ttl = await database.KeyTimeToLiveAsync(key);
        ttl!.Value.Should().BeGreaterThan(TimeSpan.FromDays(6.9), "her yazım TTL'i yeniler");
    }

    [Fact(DisplayName = "RemoveAsync anahtarı tamamen siler (yokluk = boşluk)")]
    public async Task Remove_DeletesKey()
    {
        var repository = CreateRepository();
        var customerId = Guid.NewGuid();
        await repository.SaveAsync(CartWithItems(customerId, (Guid.NewGuid(), 1)), CancellationToken.None);

        (await repository.RemoveAsync(customerId, CancellationToken.None)).IsSuccess.Should().BeTrue();

        (await fixture.Connection.GetDatabase().KeyExistsAsync($"cart:{customerId}")).Should().BeFalse();
        var loaded = await repository.GetAsync(customerId, CancellationToken.None);
        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Should().BeNull();
    }
}
