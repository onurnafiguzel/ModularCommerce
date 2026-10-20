using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol;
using Xunit;

namespace ModularCommerce.Mcp.IntegrationTests;

/// <summary>
/// place_order'ın §10.2 kanıtı (ChargeIdempotencyRaceTests/CheckoutIdempotencyRaceTests ile aynı
/// desen): aynı Idempotency-Key ile 100 PARALEL çağrı, GERÇEK Host'un GERÇEK Ordering/Inventory/
/// Payment zincirinden geçerek tek sipariş ve tek stok düşüşü üretmeli. Fake handler değil —
/// bu, ModularCommerceApiClient'ın üretim kodunun hiç değiştirilmeden gerçek bir Postgres'e karşı
/// çalıştığı tek test.
/// </summary>
[Collection(nameof(McpHostCollection))]
public sealed class PlaceOrderConcurrencyTests(McpHostFixture fixture)
{
    [Fact(DisplayName = "100 PARALEL place_order, AYNI idempotency key → DB'de tam 1 sipariş, stok tam 1 kez düşer (§10.2)")]
    public async Task HundredParallelPlaceOrders_ProduceSingleOrderAndSingleStockDecrement()
    {
        // Catalog ve Inventory BİLEREK ayrı, ilişkisiz demo veri seti kullanıyor (Catalog rastgele
        // GUID'li 15 ürün seed eder; Inventory yalnız 3 sabit well-known id için stok seed eder —
        // ikisinin kesişimi YOK). Gerçekçi bir checkout için Catalog'un seed ettiği GERÇEK bir ürüne
        // Inventory'nin dev-only stok endpoint'iyle (yalnız Development'ta açık) stok tanımlıyoruz.
        var productId = await GetFirstSeededProductIdAsync();
        await SeedInventoryStockAsync(productId, onHand: 50);

        var availableBefore = await GetAvailableStockAsync(productId);
        availableBefore.Should().Be(50);

        await fixture.ApiClient.SetCartItemAsync(productId, 1, CancellationToken.None);

        var idempotencyKey = $"mcp-concurrency-{Guid.NewGuid():N}";
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            await gate.Task;
            return await PlaceOrderUntilTerminalAsync(idempotencyKey);
        }).ToList();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        var orderIds = responses.Select(ExtractOrderId).ToList();
        orderIds.Should().OnlyContain(id => id == orderIds[0], "100 çağrının hepsi AYNI siparişe işaret etmeli");
        orderIds.Distinct().Should().ContainSingle("yalnızca TEK bir sipariş oluşmuş olmalı");

        var availableAfter = await GetAvailableStockAsync(productId);
        (availableBefore - availableAfter).Should().Be(1, "stok TAM BİR KEZ düşmeli, 100 kez değil");
    }

    /// <summary>
    /// place_order'ın KENDİ tool açıklamasının (ve StoreTools.PlaceOrder'ın hata mesajının)
    /// söylediği tam davranış: retryable=true dönerse (ör. Inventory.ConcurrencyConflict —
    /// 100-paralel aynı satıra yazan gerçek bir optimistic-concurrency yarışında BEKLENEN bir
    /// sonuç, Payment/Ordering'in KENDİ ChargeIdempotencyRaceTests/CheckoutIdempotencyRaceTests'i
    /// de AYNI şekilde "terminale kadar aynı key ile tekrar dene" yapıyor) AYNI idempotency_key
    /// ile tekrar dener; terminal (kalıcı) bir hata ise olduğu gibi fırlatır.
    /// </summary>
    private async Task<string> PlaceOrderUntilTerminalAsync(string idempotencyKey, int maxAttempts = 200)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await fixture.ApiClient.PlaceOrderAsync(idempotencyKey, CancellationToken.None);
            }
            catch (McpException ex) when (ex.Message.Contains("geçici bir hata") && attempt < maxAttempts)
            {
                // retryable=true: AYNI key ile tekrar — tool açıklamasının LLM'e söylediği kural.
            }
        }

        throw new InvalidOperationException(
            $"{maxAttempts} denemeden sonra hâlâ geçici (retryable) hata alınıyor — gerçek bir sorun olabilir.");
    }

    /// <summary>Yalnız test arrange'i — üretim ModularCommerceApiClient'ına eklenmeyen, dev-only
    /// Inventory endpoint'ine ham HttpClient ile doğrudan gider (bir MCP tool'u değil).</summary>
    private async Task SeedInventoryStockAsync(Guid productId, int onHand)
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.PutAsJsonAsync(
            $"/api/inventory/dev/stock/{productId}", new { OnHand = onHand });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> GetFirstSeededProductIdAsync()
    {
        var json = await fixture.ApiClient.SearchProductsAsync(
            query: null, page: 1, pageSize: 1, minPrice: null, maxPrice: null, CancellationToken.None);

        using var document = JsonDocument.Parse(json);
        var firstItem = document.RootElement.GetProperty("items")[0];
        return firstItem.GetProperty("id").GetGuid();
    }

    private async Task<int> GetAvailableStockAsync(Guid productId)
    {
        var json = await fixture.ApiClient.CheckStockAsync(productId, CancellationToken.None);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("available").GetInt32();
    }

    private static Guid ExtractOrderId(string orderResponseJson)
    {
        using var document = JsonDocument.Parse(orderResponseJson);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
