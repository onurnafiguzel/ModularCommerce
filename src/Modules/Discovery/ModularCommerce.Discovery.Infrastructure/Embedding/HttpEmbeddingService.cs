using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Domain.Embeddings;
using ModularCommerce.Shared.Kernel;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace ModularCommerce.Discovery.Infrastructure.Embedding;

/// <summary>
/// Gerçek embedding sağlayıcısı (OpenAI-uyumlu POST). Çağrı, adı verilen resilience pipeline'ı
/// (toplam timeout → retry+jitter → circuit breaker → deneme-başı timeout) içinde koşar. Geçici
/// hatalar (ağ, 429, 5xx, timeout) EmbeddingTransientException'a çevrilir → pipeline retry/breaker
/// görür; tükenirse retryable Result.Failure döner (istemci sonra tekrar dener).
/// </summary>
public sealed class HttpEmbeddingService(
    HttpClient httpClient,
    ResiliencePipelineProvider<string> pipelineProvider,
    IOptions<EmbeddingOptions> options) : IEmbeddingService
{
    public const string PipelineName = "embedding-api";

    private readonly EmbeddingOptions _options = options.Value;

    public async Task<Result<float[]>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var pipeline = pipelineProvider.GetPipeline(PipelineName);

        try
        {
            var vector = await pipeline.ExecuteAsync(
                async token => await CallApiAsync(text, token),
                cancellationToken);

            return Result.Success(vector);
        }
        catch (Exception ex) when (ex is EmbeddingTransientException or TimeoutRejectedException or BrokenCircuitException)
        {
            // Retry/breaker bütçesi tükendi → geçici, retryable hata.
            return Result.Failure<float[]>(DiscoveryErrors.EmbeddingUnavailable);
        }
    }

    private async Task<float[]> CallApiAsync(string text, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = JsonContent.Create(new EmbeddingApiRequest(text, _options.Model)),
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");
            }

            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new EmbeddingTransientException("Embedding API'sine ulaşılamadı.", ex);
        }

        // 429 ve 5xx geçici; retry/breaker bunları görmeli.
        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        {
            response.Dispose();
            throw new EmbeddingTransientException($"Embedding API geçici hata döndü: {(int)response.StatusCode}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            // Kalıcı (4xx) hata: retry edilmez, pipeline dışında terminal olarak yükselir.
            throw new InvalidOperationException($"Embedding API kalıcı hata döndü: {status}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingApiResponse>(cancellationToken);
        response.Dispose();

        var embedding = payload?.Data?.FirstOrDefault()?.Embedding;
        if (embedding is null || embedding.Length == 0)
        {
            throw new InvalidOperationException("Embedding API boş yanıt döndü.");
        }

        return embedding;
    }

    private sealed record EmbeddingApiRequest(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("model")] string Model);

    private sealed record EmbeddingApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<EmbeddingApiDatum>? Data);

    private sealed record EmbeddingApiDatum(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
