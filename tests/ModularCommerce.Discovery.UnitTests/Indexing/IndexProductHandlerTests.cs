using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Application.Indexing;
using ModularCommerce.Discovery.Domain.Embeddings;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Discovery.UnitTests.Indexing;

/// <summary>
/// İndeksleme: metin özeti değişmemişse embed/upsert ATLANIR (idempotent, at-least-once tekrar +
/// no-op güncellemeye karşı); değişmişse embed + upsert edilir.
/// </summary>
public sealed class IndexProductHandlerTests
{
    private readonly IEmbeddingService _embedding = Substitute.For<IEmbeddingService>();
    private readonly IProductVectorRepository _repository = Substitute.For<IProductVectorRepository>();
    private readonly IndexProductHandler _sut;

    public IndexProductHandlerTests()
    {
        _sut = new IndexProductHandler(_embedding, _repository);
        _embedding.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new float[] { 0.1f, 0.2f, 0.3f }));
    }

    private static readonly ProductIndexRequest Request =
        new(Guid.NewGuid(), "Kulaklık", "Kablosuz", "ELK-1");

    // Handler ile AYNI kuralı uygular: metin = ad + açıklama + sku (boşlukla), SHA-256 hex.
    private static string ExpectedHash(ProductIndexRequest r)
    {
        var text = string.Join(' ', new[] { r.Name, r.Description, r.Sku }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    [Fact(DisplayName = "Kaynak metin özeti aynıysa embed/upsert ATLANIR")]
    public async Task HandleAsync_HashUnchanged_SkipsEmbedAndUpsert()
    {
        _repository.GetSourceTextHashAsync(Request.ProductId, Arg.Any<CancellationToken>())
            .Returns(ExpectedHash(Request));

        var result = await _sut.HandleAsync(Request, default);

        result.IsSuccess.Should().BeTrue();
        await _embedding.DidNotReceive().EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<ProductEmbedding>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Kayıt yoksa embed edilir ve upsert edilir")]
    public async Task HandleAsync_NoExisting_EmbedsAndUpserts()
    {
        _repository.GetSourceTextHashAsync(Request.ProductId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _sut.HandleAsync(Request, default);

        result.IsSuccess.Should().BeTrue();
        await _embedding.Received(1).EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertAsync(
            Arg.Is<ProductEmbedding>(e => e.ProductId == Request.ProductId), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Embedding hatası upsert'i engeller ve hatayı iletir")]
    public async Task HandleAsync_EmbeddingFails_PropagatesAndDoesNotUpsert()
    {
        _repository.GetSourceTextHashAsync(Request.ProductId, Arg.Any<CancellationToken>()).Returns((string?)null);
        _embedding.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<float[]>(DiscoveryErrors.EmbeddingUnavailable));

        var result = await _sut.HandleAsync(Request, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscoveryErrors.EmbeddingUnavailable);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<ProductEmbedding>(), Arg.Any<CancellationToken>());
    }
}
