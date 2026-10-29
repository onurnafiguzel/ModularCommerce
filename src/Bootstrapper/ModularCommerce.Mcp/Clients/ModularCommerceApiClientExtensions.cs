using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace ModularCommerce.Mcp.Clients;

public static class ModularCommerceApiClientExtensions
{
    /// <summary>
    /// Resilience pipeline'ı + named HttpClient'ı + ModularCommerceApiClient'ı kaydeder. Tek
    /// doğruluk kaynağı: testler de AYNI bu metodu çağırır (farklı bir fake handler'la) — üretim
    /// ve test arasında pipeline konfigürasyonu drift etme riski olmaz.
    /// </summary>
    public static IHttpClientBuilder AddModularCommerceApiClient(
        this IServiceCollection services, string baseUrl)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(
            ModularCommerceApiClient.PipelineName, pipeline =>
            {
                var transientErrors = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(response =>
                        (int)response.StatusCode >= 500
                        || response.StatusCode == HttpStatusCode.RequestTimeout
                        || response.StatusCode == HttpStatusCode.TooManyRequests);

                pipeline
                    .AddTimeout(TimeSpan.FromSeconds(3))
                    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                    {
                        ShouldHandle = transientErrors,
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromMilliseconds(200),
                    })
                    .AddTimeout(TimeSpan.FromSeconds(1));
            });

        var httpClientBuilder = services.AddHttpClient(
            ModularCommerceApiClient.HttpClientName,
            client => client.BaseAddress = new Uri(baseUrl));

        services.AddSingleton<ModularCommerceApiClient>();

        return httpClientBuilder;
    }
}
