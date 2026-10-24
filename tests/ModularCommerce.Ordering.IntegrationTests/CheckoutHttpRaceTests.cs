using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ModularCommerce.Ordering.IntegrationTests.Fixtures;
using Npgsql;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

/// <summary>
/// §10.2'nin GERÇEK HTTP kanıtı (Mcp.IntegrationTests/PlaceOrderConcurrencyTests ile aynı desen,
/// MCP katmanı YOK — doğrudan Host'un public API'sine): aynı Idempotency-Key ile 100 PARALEL
/// POST /api/ordering/checkout, GERÇEK Ordering/Inventory/Payment/Catalog/Cart zincirinden geçerek
/// tek sipariş + tek stok düşüşü üretmeli. Bu test, Order.Id'nin DETERMİNİSTİK olmasının
/// (DeterministicOrderId, bu branch'e taşındı) Payment'ın OWN OrderId kontrolüyle (bu branch'te
/// e450e79'dan beri var) BİRLİKTE güvenli olduğunu — yani e450e79 TEK BAŞINA (determinizm olmadan)
/// bu testi FAIL ettireceğini — kanıtlar.
/// </summary>
[Collection(nameof(HostStackCollection))]
public sealed class CheckoutHttpRaceTests(HostStackFixture fixture)
{
    [Fact(DisplayName = "100 PARALEL POST /checkout, AYNI Idempotency-Key → DB'de tam 1 sipariş, stok tam 1 kez düşer, Payment.OrderId == kazanan Order.Id (§10.2)")]
    public async Task HundredParallelCheckouts_ProduceSingleOrder_PaymentOrderIdMatchesWinner()
    {
        var productId = await GetFirstSeededProductIdAsync();
        await SeedInventoryStockAsync(productId, onHand: 50);

        var availableBefore = await GetAvailableStockAsync(productId);
        availableBefore.Should().Be(50);

        await AddToCartAsync(productId, quantity: 1);

        var idempotencyKey = $"http-concurrency-{Guid.NewGuid():N}";
        var gate = new TaskCompletionSource();

        // Burada BİLEREK retry YOK (MCP tarafındaki PlaceOrderUntilTerminalAsync'in aksine):
        // "checkout" rate-limit policy'si TAM OLARAK bu testin kendi deseni için (permit+queue
        // ≥ 100, tek seferlik 100-paralel) kalibre edilmiş — retry ile toplam istek sayısı
        // kolayca 100'ü aşıp 429'a çarpar (gözlemlendi). Kaybeden denemelerin bir kısmı retryable
        // (409) dönebilir, bu testin odağı değil — yeter ki EN AZ biri kazansın (ki kazanır).
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            await gate.Task;

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ordering/checkout");
            request.Headers.Add("Idempotency-Key", idempotencyKey);

            var response = await fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, Body: body);
        }).ToList();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        var orderIds = responses.Where(r => r.IsSuccessStatusCode).Select(r => ExtractOrderId(r.Body)).ToList();
        orderIds.Should().NotBeEmpty("en az bir çağrı başarıyla sonuçlanmalı");
        orderIds.Should().OnlyContain(id => id == orderIds[0], "başarılı çağrıların hepsi AYNI siparişe işaret etmeli");
        var winningOrderId = orderIds.Distinct().Should().ContainSingle("yalnızca TEK bir sipariş oluşmuş olmalı").Which;

        var availableAfter = await GetAvailableStockAsync(productId);
        (availableBefore - availableAfter).Should().Be(1, "stok TAM BİR KEZ düşmeli, 100 kez değil");

        // Payment ve Ordering'in BAĞIMSIZ hakemleri (iki farklı unique index) AYNI OrderId'de
        // anlaşmalı — bu, deterministik Order.Id'nin asıl kanıtladığı şey.
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();

        var paymentOrderIds = await QueryAsync<Guid>(connection,
            "SELECT \"OrderId\" FROM payment.payments WHERE \"CustomerId\" = @customerId AND \"IdempotencyKey\" = @key",
            ("customerId", HostStackFixture.TestCustomerId), ("key", idempotencyKey));
        paymentOrderIds.Should().ContainSingle("bu key için TEK bir ödeme satırı olmalı")
            .Which.Should().Be(winningOrderId, "ödeme satırı KAZANAN siparişe ait olmalı (Payment ve Ordering'in hakemleri anlaşmalı)");
    }

    private async Task AddToCartAsync(Guid productId, int quantity)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/cart/items", new { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Yalnız test arrange'i — dev-only Inventory endpoy'ine doğrudan gider.</summary>
    private async Task SeedInventoryStockAsync(Guid productId, int onHand)
    {
        var response = await fixture.Client.PutAsJsonAsync($"/api/inventory/dev/stock/{productId}", new { OnHand = onHand });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> GetFirstSeededProductIdAsync()
    {
        var json = await fixture.Client.GetStringAsync("/api/catalog/products?page=1&pageSize=1");
        using var document = JsonDocument.Parse(json);
        var firstItem = document.RootElement.GetProperty("items")[0];
        return firstItem.GetProperty("id").GetGuid();
    }

    private async Task<int> GetAvailableStockAsync(Guid productId)
    {
        var json = await fixture.Client.GetStringAsync($"/api/inventory/stock/{productId}");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("available").GetInt32();
    }

    private static Guid ExtractOrderId(string orderResponseJson)
    {
        using var document = JsonDocument.Parse(orderResponseJson);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<List<T>> QueryAsync<T>(
        NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add((T)reader.GetValue(0));
        }

        return results;
    }
}
