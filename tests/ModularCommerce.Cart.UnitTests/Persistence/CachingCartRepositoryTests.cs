using FluentAssertions;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Cart.Infrastructure.Caching;
using ModularCommerce.Cart.Infrastructure.Persistence;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Persistence;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

/// <summary>
/// Decorator davranışı: KAYNAK Postgres (inner), Redis (cache) hızlandırıcı. Cache hit → DB'ye gitmez;
/// miss → DB + cache doldurulur; yazma önce DB, başarılıysa cache (çift-yazma tutarsızlığı yok); DB
/// başarısızsa cache'e HİÇ dokunulmaz.
/// </summary>
public sealed class CachingCartRepositoryTests
{
    private readonly ICartRepository _inner = Substitute.For<ICartRepository>();
    private readonly ICartCache _cache = Substitute.For<ICartCache>();
    private readonly CachingCartRepository _sut;

    public CachingCartRepositoryTests() => _sut = new CachingCartRepository(_inner, _cache);

    private static Cart CartFor(Guid customerId)
    {
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(Guid.NewGuid(), 1);
        return cart;
    }

    [Fact(DisplayName = "Get cache hit: DB'ye (inner) HİÇ gidilmez")]
    public async Task Get_CacheHit_DoesNotTouchSource()
    {
        var id = Guid.NewGuid();
        _cache.GetAsync(id, Arg.Any<CancellationToken>()).Returns(CartFor(id));

        var result = await _sut.GetAsync(id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CustomerId.Should().Be(id);
        await _inner.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Get cache miss: Postgres'ten okunur ve cache doldurulur")]
    public async Task Get_CacheMiss_ReadsSourceAndPopulatesCache()
    {
        var id = Guid.NewGuid();
        _cache.GetAsync(id, Arg.Any<CancellationToken>()).Returns((Cart?)null); // miss (ya da Redis-down)
        _inner.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Result.Success<Cart?>(CartFor(id)));

        var result = await _sut.GetAsync(id, CancellationToken.None);

        result.Value.Should().NotBeNull();
        await _inner.Received(1).GetAsync(id, Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(Arg.Is<Cart>(c => c.CustomerId == id), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Get: kaynakta da yoksa cache doldurulmaz")]
    public async Task Get_MissAndSourceNull_DoesNotCache()
    {
        var id = Guid.NewGuid();
        _cache.GetAsync(id, Arg.Any<CancellationToken>()).Returns((Cart?)null);
        _inner.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Result.Success<Cart?>(null));

        var result = await _sut.GetAsync(id, CancellationToken.None);

        result.Value.Should().BeNull();
        await _cache.DidNotReceive().SetAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Save: önce Postgres, başarılıysa cache set edilir")]
    public async Task Save_WritesSourceThenCache()
    {
        var cart = CartFor(Guid.NewGuid());
        _inner.SaveAsync(cart, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await _sut.SaveAsync(cart, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _inner.Received(1).SaveAsync(cart, Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(cart, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Save: Postgres başarısızsa cache'e HİÇ dokunulmaz (tutarlılık)")]
    public async Task Save_SourceFails_DoesNotTouchCache()
    {
        var cart = CartFor(Guid.NewGuid());
        _inner.SaveAsync(cart, Arg.Any<CancellationToken>()).Returns(Result.Failure(CartErrors.StorageUnavailable));

        var result = await _sut.SaveAsync(cart, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _cache.DidNotReceive().SetAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remove: önce Postgres, başarılıysa cache'ten de silinir")]
    public async Task Remove_DeletesSourceThenCache()
    {
        var id = Guid.NewGuid();
        _inner.RemoveAsync(id, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await _sut.RemoveAsync(id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _cache.Received(1).RemoveAsync(id, Arg.Any<CancellationToken>());
    }
}
