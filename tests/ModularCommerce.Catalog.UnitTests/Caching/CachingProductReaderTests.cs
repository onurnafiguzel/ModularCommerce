using FluentAssertions;
using ModularCommerce.Catalog.Contracts;
using ModularCommerce.Catalog.Infrastructure.Caching;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Caching;

/// <summary>
/// CachingProductReader decorator'ı (checkout sıcak yolu): per-id snapshot cache; kısmi isabette
/// yalnız cache'te olmayan id'ler DB'ye gider; tam isabette DB'ye HİÇ gidilmez.
/// </summary>
public sealed class CachingProductReaderTests
{
    private readonly IProductReader _inner = Substitute.For<IProductReader>();
    private readonly FakeProductCache _cache = new();

    private static ProductSnapshotDto Snapshot(Guid id) => new(id, "Ürün", 100m, "TRY", true);

    [Fact(DisplayName = "Kısmi isabet: yalnız cache'te olmayan id'ler DB'den okunur ve cache'lenir")]
    public async Task PartialHit_FetchesOnlyMissesAndCachesThem()
    {
        var cachedId = Guid.NewGuid();
        var missId = Guid.NewGuid();
        _cache.Seed(CatalogCacheKeys.Snapshot(cachedId), Snapshot(cachedId));
        _inner.GetByIdsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(missId)),
                Arg.Any<CancellationToken>())
            .Returns(new[] { Snapshot(missId) });

        var sut = new CachingProductReader(_inner, _cache);
        var result = await sut.GetByIdsAsync(new[] { cachedId, missId }, CancellationToken.None);

        result.Select(s => s.ProductId).Should().BeEquivalentTo(new[] { cachedId, missId });
        await _inner.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(missId)),
            Arg.Any<CancellationToken>());
        _cache.Writes.Should().Be(1, "yalnız miss cache'lenir");
    }

    [Fact(DisplayName = "Tam isabet: DB'ye HİÇ gidilmez")]
    public async Task AllHit_DoesNotTouchInner()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _cache.Seed(CatalogCacheKeys.Snapshot(id1), Snapshot(id1));
        _cache.Seed(CatalogCacheKeys.Snapshot(id2), Snapshot(id2));

        var sut = new CachingProductReader(_inner, _cache);
        var result = await sut.GetByIdsAsync(new[] { id1, id2 }, CancellationToken.None);

        result.Should().HaveCount(2);
        await _inner.DidNotReceive().GetByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }
}
