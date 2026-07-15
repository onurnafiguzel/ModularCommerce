using FluentValidation;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Application.Common;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Discovery.Application.Search;

/// <summary>
/// Doğal dil sorgusunu embed eder ve saklanan ürün vektörlerine karşı kosinüs benzerliğiyle
/// en yakın topN ürünü döner. Embedding sağlayıcısı düşerse retryable hata döner.
/// </summary>
public sealed class SearchProductsHandler(
    IEmbeddingService embeddingService,
    IProductVectorRepository repository,
    IValidator<SearchQuery> validator)
{
    public async Task<Result<IReadOnlyList<SearchResultResponse>>> HandleAsync(
        SearchQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<IReadOnlyList<SearchResultResponse>>(Error.Validation(
                "Discovery.Search.InvalidQuery",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var embedding = await embeddingService.EmbedAsync(query.Query, cancellationToken);
        if (embedding.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SearchResultResponse>>(embedding.Error);
        }

        var matches = await repository.SearchAsync(embedding.Value, query.TopN, cancellationToken);

        return Result.Success<IReadOnlyList<SearchResultResponse>>(
            [.. matches.Select(m => new SearchResultResponse(m.ProductId, m.Score))]);
    }
}
