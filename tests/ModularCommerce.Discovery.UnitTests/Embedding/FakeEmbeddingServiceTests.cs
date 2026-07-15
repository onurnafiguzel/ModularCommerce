using FluentAssertions;
using ModularCommerce.Discovery.Infrastructure.Embedding;
using Xunit;

namespace ModularCommerce.Discovery.UnitTests.Embedding;

/// <summary>
/// Fake embedding: (a) deterministik (aynı metin → aynı vektör), (b) doğru boyut, (c) ortak-terimli
/// metinler benzer (feature hashing) → arama gerçekten çalışır; ayrık terimler ilgisiz.
/// </summary>
public sealed class FakeEmbeddingServiceTests
{
    private readonly FakeEmbeddingService _sut = new();

    [Fact(DisplayName = "Aynı metin her zaman aynı vektörü üretir (deterministik)")]
    public async Task EmbedAsync_SameText_SameVector()
    {
        var a = (await _sut.EmbedAsync("kablosuz kulaklık", default)).Value;
        var b = (await _sut.EmbedAsync("kablosuz kulaklık", default)).Value;

        a.Should().Equal(b);
    }

    [Fact(DisplayName = "Vektör boyutu EmbeddingConstants.Dimensions'a eşittir")]
    public async Task EmbedAsync_ReturnsConfiguredDimension()
    {
        var vector = (await _sut.EmbedAsync("herhangi bir metin", default)).Value;

        vector.Should().HaveCount(EmbeddingConstants.Dimensions);
    }

    [Fact(DisplayName = "Ortak terim paylaşan metinler, ayrık metinlerden daha benzer")]
    public async Task EmbedAsync_SharedToken_MoreSimilarThanDisjoint()
    {
        var query = (await _sut.EmbedAsync("kulaklık", default)).Value;
        var related = (await _sut.EmbedAsync("kablosuz kulaklık x200", default)).Value; // "kulaklık" ortak
        var unrelated = (await _sut.EmbedAsync("yoga matı", default)).Value;            // ayrık

        Cosine(query, related).Should().BeGreaterThan(Cosine(query, unrelated));
        Cosine(query, related).Should().BeGreaterThan(0);
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * (double)b[i];
        }

        return dot; // her iki vektör L2-normalize olduğundan nokta çarpım = kosinüs benzerliği.
    }
}
