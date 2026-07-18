using FluentAssertions;
using ModularCommerce.Cart.Infrastructure.Persistence;
using ModularCommerce.Cart.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Cart.IntegrationTests;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

/// <summary>
/// Sepetin KAYNAK repository'si (Postgres, jsonb kalemler): round-trip, olmayan müşteri → null, silme.
/// Her işlem AYRI DbContext ile — bağlantılar arası kalıcılık kanıtlanır (checkout Redis'siz de okur).
/// </summary>
[Collection("CartPostgres")]
public sealed class PostgresCartRepositoryTests(PostgresContainerFixture fixture)
{
    private PostgresCartRepository Repository() => new(fixture.CreateContext());

    private static Cart CartWithItems(Guid customerId, params (Guid ProductId, int Quantity)[] lines)
    {
        var cart = Cart.Create(customerId).Value;
        foreach (var (productId, quantity) in lines)
        {
            cart.AddItem(productId, quantity).IsSuccess.Should().BeTrue();
        }

        return cart;
    }

    [Fact(DisplayName = "Round-trip: kaydedilen sepet ayrı bağlantıda satırlarıyla aynen okunur")]
    public async Task SaveAndGet_RoundTripsAcrossContexts()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        (await Repository().SaveAsync(CartWithItems(customerId, (productId, 3)), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var loaded = await Repository().GetAsync(customerId, CancellationToken.None);

        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.CustomerId.Should().Be(customerId);
        loaded.Value.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 3);
    }

    [Fact(DisplayName = "İkinci kayıt sepeti günceller (upsert, PK=CustomerId)")]
    public async Task Save_Twice_Upserts()
    {
        var customerId = Guid.NewGuid();
        await Repository().SaveAsync(CartWithItems(customerId, (Guid.NewGuid(), 1)), CancellationToken.None);
        await Repository().SaveAsync(CartWithItems(customerId, (Guid.NewGuid(), 2), (Guid.NewGuid(), 4)), CancellationToken.None);

        var loaded = await Repository().GetAsync(customerId, CancellationToken.None);

        loaded.Value!.Items.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Hiç kaydedilmemiş sepet: başarılı ve null")]
    public async Task Get_UnknownCustomer_ReturnsSuccessNull()
    {
        var loaded = await Repository().GetAsync(Guid.NewGuid(), CancellationToken.None);

        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Should().BeNull();
    }

    [Fact(DisplayName = "RemoveAsync satırı siler (yokluk = boş sepet)")]
    public async Task Remove_DeletesRow()
    {
        var customerId = Guid.NewGuid();
        await Repository().SaveAsync(CartWithItems(customerId, (Guid.NewGuid(), 1)), CancellationToken.None);

        (await Repository().RemoveAsync(customerId, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var loaded = await Repository().GetAsync(customerId, CancellationToken.None);
        loaded.Value.Should().BeNull();
    }
}
