using FluentAssertions;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Application.Search;
using ModularCommerce.Discovery.Domain.Embeddings;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Discovery.UnitTests.Search;

/// <summary>
/// Arama: sorguyu doğrular, embed eder, repo'dan gelen eşleşmeleri yanıta çevirir; embedding hatası
/// (geçici) retryable olarak iletilir.
/// </summary>
public sealed class SearchProductsHandlerTests
{
    private readonly IEmbeddingService _embedding = Substitute.For<IEmbeddingService>();
    private readonly IProductVectorRepository _repository = Substitute.For<IProductVectorRepository>();
    private readonly SearchProductsHandler _sut;

    public SearchProductsHandlerTests()
        => _sut = new SearchProductsHandler(_embedding, _repository, new SearchQueryValidator());

    [Fact(DisplayName = "Boş sorgu doğrulamadan geçmez (embed edilmez)")]
    public async Task HandleAsync_EmptyQuery_FailsValidation()
    {
        var result = await _sut.HandleAsync(new SearchQuery("", 5), default);

        result.IsFailure.Should().BeTrue();
        await _embedding.DidNotReceive().EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "TopN aralık dışıysa reddedilir (1..50)")]
    public async Task HandleAsync_TopNOutOfRange_FailsValidation()
    {
        var result = await _sut.HandleAsync(new SearchQuery("kulaklık", 999), default);

        result.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Geçerli sorgu: embed → search → yanıt eşlenir")]
    public async Task HandleAsync_Valid_MapsMatches()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        _embedding.EmbedAsync("kulaklık", Arg.Any<CancellationToken>())
            .Returns(Result.Success(new[] { 0.1f, 0.2f }));
        _repository.SearchAsync(Arg.Any<float[]>(), 5, Arg.Any<CancellationToken>())
            .Returns(new List<VectorMatch> { new(p1, 0.9), new(p2, 0.7) });

        var result = await _sut.HandleAsync(new SearchQuery("kulaklık", 5), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].ProductId.Should().Be(p1);
        result.Value[0].Score.Should().Be(0.9);
    }

    [Fact(DisplayName = "Embedding geçici hatası retryable olarak iletilir")]
    public async Task HandleAsync_EmbeddingFails_PropagatesError()
    {
        _embedding.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<float[]>(DiscoveryErrors.EmbeddingUnavailable));

        var result = await _sut.HandleAsync(new SearchQuery("kulaklık", 5), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Retryable.Should().BeTrue();
    }
}
