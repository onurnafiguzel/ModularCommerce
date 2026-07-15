using FluentAssertions;
using ModularCommerce.Discovery.Domain.Embeddings;
using ModularCommerce.Discovery.Infrastructure.Embedding;
using ModularCommerce.Discovery.Infrastructure.Persistence;
using Xunit;

namespace ModularCommerce.Discovery.IntegrationTests;

/// <summary>
/// Gerçek pgvector'a karşı: upsert (idempotent PK), kaynak-hash okuma ve KOSİNÜS araması sıralaması.
/// Gerçek Fake embedding + gerçek `vector` kolonu + `&lt;=&gt;` operatörü — raw SQL yolu uçtan uca doğrulanır.
/// </summary>
public sealed class ProductVectorRepositoryTests(PgVectorFixture fixture) : IClassFixture<PgVectorFixture>
{
    private readonly FakeEmbeddingService _embedding = new();

    private async Task<float[]> Embed(string text) => (await _embedding.EmbedAsync(text, default)).Value;

    [Fact(DisplayName = "Upsert satır yazar; GetSourceTextHash yazılan özeti döner; tekrar upsert günceller")]
    public async Task Upsert_IsIdempotentByProductId()
    {
        var repo = new ProductVectorRepository(fixture.DataSource);
        var id = Guid.NewGuid();

        var first = ProductEmbedding.Create(id, await Embed("kulaklık"), "hash-v1").Value;
        await repo.UpsertAsync(first, default);
        (await repo.GetSourceTextHashAsync(id, default)).Should().Be("hash-v1");

        // Aynı PK ile tekrar → INSERT değil UPDATE (idempotent).
        var second = ProductEmbedding.Create(id, await Embed("klavye"), "hash-v2").Value;
        await repo.UpsertAsync(second, default);
        (await repo.GetSourceTextHashAsync(id, default)).Should().Be("hash-v2");
    }

    [Fact(DisplayName = "Arama, sorguya en yakın ürünü kosinüs benzerliğiyle ilk sırada döner")]
    public async Task Search_RanksNearestFirst()
    {
        var repo = new ProductVectorRepository(fixture.DataSource);
        var headphones = Guid.NewGuid();
        var keyboard = Guid.NewGuid();
        var book = Guid.NewGuid();

        await repo.UpsertAsync(ProductEmbedding.Create(headphones, await Embed("kablosuz kulaklık x200"), "h1").Value, default);
        await repo.UpsertAsync(ProductEmbedding.Create(keyboard, await Embed("mekanik klavye pro"), "h2").Value, default);
        await repo.UpsertAsync(ProductEmbedding.Create(book, await Embed("temiz kod kitabı"), "h3").Value, default);

        var matches = await repo.SearchAsync(await Embed("kulaklık"), topN: 3, default);

        matches.Should().NotBeEmpty();
        matches[0].ProductId.Should().Be(headphones, "‘kulaklık’ terimini paylaşan ürün en yakın olmalı");
        matches[0].Score.Should().BeGreaterThan(matches[^1].Score);
    }
}
