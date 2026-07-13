using FluentAssertions;
using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Infrastructure.Caching;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Caching;

/// <summary>
/// CachingProductQueries decorator'ı: detay okuması read-through cache'lenir (miss→DB+yaz,
/// hit→DB'ye HİÇ gitmez); liste passthrough kalır (cache'lenmez).
/// </summary>
public sealed class CachingProductQueriesTests
{
    private readonly IProductQueries _inner = Substitute.For<IProductQueries>();
    private readonly FakeProductCache _cache = new();

    private static ProductDetailResponse Detail(Guid id) =>
        new(id, "Ürün", "Açıklama", "SKU-1", 100m, "TRY", 10, true, DateTime.UtcNow);

    [Fact(DisplayName = "Detay cache miss: DB'ye gidilir ve sonuç cache'e yazılır")]
    public async Task Detail_OnMiss_LoadsFromInnerAndCaches()
    {
        var id = Guid.NewGuid();
        var detail = Detail(id);
        _inner.GetProductByIdAsync(id, Arg.Any<CancellationToken>()).Returns(detail);

        var sut = new CachingProductQueries(_inner, _cache);
        var result = await sut.GetProductByIdAsync(id, CancellationToken.None);

        result.Should().BeSameAs(detail);
        await _inner.Received(1).GetProductByIdAsync(id, Arg.Any<CancellationToken>());
        _cache.Writes.Should().Be(1, "miss sonrası cache doldurulur");
    }

    [Fact(DisplayName = "Detay cache hit: DB'ye HİÇ gidilmez")]
    public async Task Detail_OnHit_DoesNotTouchInner()
    {
        var id = Guid.NewGuid();
        var detail = Detail(id);
        _cache.Seed(CatalogCacheKeys.Product(id), detail);

        var sut = new CachingProductQueries(_inner, _cache);
        var result = await sut.GetProductByIdAsync(id, CancellationToken.None);

        result.Should().BeSameAs(detail);
        await _inner.DidNotReceive().GetProductByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ürün yoksa cache'e yazılmaz (negatif sonuç cache'lenmez)")]
    public async Task Detail_WhenMissing_DoesNotCache()
    {
        var id = Guid.NewGuid();
        _inner.GetProductByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ProductDetailResponse?)null);

        var sut = new CachingProductQueries(_inner, _cache);
        var result = await sut.GetProductByIdAsync(id, CancellationToken.None);

        result.Should().BeNull();
        _cache.Writes.Should().Be(0);
    }
}
