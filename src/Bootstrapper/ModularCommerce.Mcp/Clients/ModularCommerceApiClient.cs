using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol;
using Polly.Registry;

namespace ModularCommerce.Mcp.Clients;

/// <summary>
/// ModularCommerce.Host'un HTTP API'sine giden tüm çağrıların tek giriş noktası. Her metod,
/// resilience pipeline'ın HER denemesinde TAZE bir HttpRequestMessage üretir (bir kez
/// gönderilen HttpRequestMessage tekrar gönderilemez) — ama header DEĞERLERİ (özellikle
/// place_order'ın Idempotency-Key'i) requestFactory'ye closure ile capture edilir, yani her
/// denemede AYNI değer taşınır; idempotency key retry döngüsünün İÇİNDE asla yeniden üretilmez.
/// </summary>
public sealed class ModularCommerceApiClient(
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider)
{
    public const string HttpClientName = "ModularCommerceApi";
    public const string PipelineName = "modularcommerce-api";

    public Task<string> SearchProductsAsync(
        string? query, int page, int pageSize, decimal? minPrice, decimal? maxPrice,
        CancellationToken cancellationToken)
    {
        var queryString = BuildQueryString(
            ("search", query),
            ("page", page.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            ("minPrice", minPrice?.ToString(CultureInfo.InvariantCulture)),
            ("maxPrice", maxPrice?.ToString(CultureInfo.InvariantCulture)));

        return SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/products{queryString}"),
            cancellationToken);
    }

    public Task<string> CheckStockAsync(Guid productId, CancellationToken cancellationToken)
        => SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"/api/inventory/stock/{productId}"),
            cancellationToken);

    public Task<string> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken)
        => SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"/api/ordering/orders/{orderId}"),
            cancellationToken);

    public Task<string> GetCartAsync(CancellationToken cancellationToken)
        => SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "/api/cart"), cancellationToken);

    /// <summary>
    /// Sepetteki bir ürünün miktarını TAM OLARAK <paramref name="quantity"/>'ye ayarlar
    /// (artırma değil, "ayarlama" semantiği) — idempotenttir, aynı çağrı art arda tekrarlansa
    /// bile sepet bozulmaz. Altta yatan API'de bu tam karşılığıyla YOK: PUT /items/{id} mevcut
    /// bir satırı istenen değere SET eder (idempotent) ama satır sepette yoksa ItemNotFound
    /// döner; POST /items ise ARTIRMALI çalışır (var olan miktara ekler). Bu yüzden önce
    /// GetCart ile ürünün sepette olup olmadığına bakılır, sonra duruma göre doğru fiil
    /// (PUT/POST/DELETE) seçilir — POST yalnız ürün sepette HİÇ YOKKEN çağrılır, o durumda
    /// "artırma" sıfırdan başladığı için fiilen "ayarlama"yla aynı sonucu verir.
    /// </summary>
    public async Task<string> SetCartItemAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var currentCartJson = await GetCartAsync(cancellationToken);
        var alreadyInCart = CartContainsProduct(currentCartJson, productId);

        if (quantity <= 0)
        {
            if (!alreadyInCart)
            {
                // Zaten sepette yok — istenen durum (yok) zaten sağlanmış, no-op (idempotent).
                return currentCartJson;
            }

            return await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, $"/api/cart/items/{productId}"),
                cancellationToken);
        }

        if (alreadyInCart)
        {
            return await SendAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"/api/cart/items/{productId}");
                request.Content = JsonContent.Create(new { Quantity = quantity });
                return request;
            }, cancellationToken);
        }

        return await SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/items");
            request.Content = JsonContent.Create(new { ProductId = productId, Quantity = quantity });
            return request;
        }, cancellationToken);
    }

    private static bool CartContainsProduct(string cartJson, Guid productId)
    {
        using var document = JsonDocument.Parse(cartJson);
        if (!document.RootElement.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("productId", out var idElement)
                && idElement.TryGetGuid(out var id)
                && id == productId)
            {
                return true;
            }
        }

        return false;
    }

    public Task<string> PlaceOrderAsync(string idempotencyKey, CancellationToken cancellationToken)
        => SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/ordering/checkout");
            // idempotencyKey burada closure ile yakalanıyor — SendAsync'in her retry denemesinde
            // requestFactory tekrar çağrılır ama idempotencyKey parametre olarak SABİT kalır,
            // asla burada yeniden üretilmez.
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            return request;
        }, cancellationToken);

    private async Task<string> SendAsync(
        Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(PipelineName);
        using var client = httpClientFactory.CreateClient(HttpClientName);

        var response = await pipeline.ExecuteAsync(async token =>
        {
            using var request = requestFactory();
            return await client.SendAsync(request, token);
        }, cancellationToken);

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            throw ToMcpException(response.StatusCode, body);
        }
    }

    private static string BuildQueryString(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}")
            .ToArray();

        return parts.Length == 0 ? string.Empty : $"?{string.Join('&', parts)}";
    }

    /// <summary>
    /// API'nin ProblemDetails gövdesini (code/detail/retryable) LLM'in anlayacağı bir mesaja
    /// çevirir. retryable bilgisi genel tutulur (idempotency_key'den burada bahsedilmez —
    /// bu yalnız place_order'a özgü, çağıran StoreTools.PlaceOrder bunu kendi ekler).
    /// </summary>
    private static McpException ToMcpException(HttpStatusCode statusCode, string body)
    {
        string? code = null;
        string? detail = null;
        var retryable = false;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("code", out var codeElement))
            {
                code = codeElement.GetString();
            }

            if (root.TryGetProperty("detail", out var detailElement))
            {
                detail = detailElement.GetString();
            }

            if (root.TryGetProperty("retryable", out var retryableElement)
                && retryableElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                retryable = retryableElement.GetBoolean();
            }
        }
        catch (JsonException)
        {
            // Gövde ProblemDetails değil (beklenmeyen bir hata) — ham gövdeyi mesaja koy.
        }

        var retryHint = retryable
            ? "Bu geçici bir hata (retryable=true) — isteği tekrar deneyebilirsin."
            : "Bu kalıcı bir hata (retryable=false) — aynı girdilerle tekrar denemek sonucu değiştirmez.";

        var summary = code is not null ? $"{code}: {detail ?? body}" : $"HTTP {(int)statusCode}: {body}";

        return new McpException($"{summary}. {retryHint}");
    }
}
