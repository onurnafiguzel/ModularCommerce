using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularCommerce.Catalog.Contracts;
using ModularCommerce.Catalog.Infrastructure.Caching;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Caching;

/// <summary>
/// RedisProductCache graceful degradation kanıtı: Redis bağlantı/timeout hatasında okuma miss gibi
/// davranır (null → çağıran DB'ye düşer), yazma sessizce atlanır → cache ASLA okuma yolunu kırmaz.
/// Ayrıca değer varsa JSON round-trip doğru deserialize edilir.
/// </summary>
public sealed class RedisProductCacheTests
{
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly RedisProductCache _sut;

    public RedisProductCacheTests()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(_db);
        _sut = new RedisProductCache(
            redis,
            Options.Create(new CatalogCacheOptions { TtlSeconds = 60 }),
            NullLogger<RedisProductCache>.Instance);
    }

    private static readonly RedisConnectionException Down =
        new(ConnectionFailureType.UnableToConnect, "redis down");

    [Fact(DisplayName = "Redis erişilemezse GetAsync null döner (miss gibi, DB'ye düşülür)")]
    public async Task GetAsync_WhenConnectionFails_ReturnsNull()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>()).ThrowsAsyncForAnyArgs(Down);

        var result = await _sut.GetAsync<ProductSnapshotDto>("k", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "Değer varsa GetAsync JSON'ı doğru deserialize eder")]
    public async Task GetAsync_WhenValuePresent_Deserializes()
    {
        var id = Guid.NewGuid();
        var json = System.Text.Json.JsonSerializer.Serialize(
            new ProductSnapshotDto(id, "Ürün", 100m, "TRY", true),
            System.Text.Json.JsonSerializerOptions.Web);
        _db.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)json);

        var result = await _sut.GetAsync<ProductSnapshotDto>(CatalogCacheKeys.Snapshot(id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ProductId.Should().Be(id);
        result.Price.Should().Be(100m);
    }

    [Fact(DisplayName = "Enabled=false: cache passthrough — Redis'e HİÇ dokunulmaz")]
    public async Task WhenDisabled_IsPurePassthrough()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(_db);
        var disabled = new RedisProductCache(
            redis,
            Options.Create(new CatalogCacheOptions { Enabled = false }),
            NullLogger<RedisProductCache>.Instance);

        var got = await disabled.GetAsync<ProductSnapshotDto>("k", CancellationToken.None);
        await disabled.SetAsync("k", new ProductSnapshotDto(Guid.NewGuid(), "Ürün", 1m, "TRY", true), CancellationToken.None);

        got.Should().BeNull("kapalıyken her okuma miss olur → çağıran DB'ye düşer");
        redis.DidNotReceiveWithAnyArgs().GetDatabase();
    }

    [Fact(DisplayName = "Redis erişilemezse SetAsync fırlatmaz (best-effort)")]
    public async Task SetAsync_WhenConnectionFails_DoesNotThrow()
    {
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>())
            .ThrowsAsyncForAnyArgs(Down);

        var act = async () => await _sut.SetAsync(
            "k", new ProductSnapshotDto(Guid.NewGuid(), "Ürün", 1m, "TRY", true), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
