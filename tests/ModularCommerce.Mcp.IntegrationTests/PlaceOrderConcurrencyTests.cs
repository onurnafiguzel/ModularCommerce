using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using FluentAssertions;
using ModelContextProtocol;
using Npgsql;
using Polly.Timeout;
using Xunit;
using Xunit.Abstractions;

namespace ModularCommerce.Mcp.IntegrationTests;

/// <summary>
/// place_order'ın §10.2 kanıtı (ChargeIdempotencyRaceTests/CheckoutIdempotencyRaceTests ile aynı
/// desen): aynı Idempotency-Key ile 100 PARALEL çağrı, GERÇEK Host'un GERÇEK Ordering/Inventory/
/// Payment zincirinden geçerek tek sipariş ve tek stok düşüşü üretmeli. Fake handler değil —
/// bu, ModularCommerceApiClient'ın üretim kodunun hiç değiştirilmeden gerçek bir Postgres'e karşı
/// çalıştığı tek test.
/// </summary>
[Collection(nameof(McpHostCollection))]
public sealed class PlaceOrderConcurrencyTests(McpHostFixture fixture, ITestOutputHelper output)
{
    private int _transportRetryCount;


    // "Stress": PR CI'ı bu kategoriyi hariç tutar (ci.yml) — 100-paralel gerçek Testcontainers
    // koşusu hem yavaş hem bilinen bir TTL-ile-kendiliğinden-düzelen sızıntıya karşı (bkz.
    // FOLLOWUPS.md) gürültülü. Haftalık + workflow_dispatch ile ayrı bir workflow'da çalışır
    // (stress-tests.yml).
    [Trait("Category", "Stress")]
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

        // Yetim kayıt kontrolü: kaybeden 99 denemenin HER BİRİ kendi TAZE OrderId'sini üretti
        // (Order.Create, CheckoutHandler) — bunlardan HİÇBİRİNE bağlı Payment/rezervasyon/outbox
        // kaydı kalmamalı; sadece KAZANAN OrderId'ye ait kayıtlar var olmalı.
        var winningOrderId = orderIds[0];
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();

        var paymentRows = await QueryAsync<Guid>(connection,
            "SELECT \"OrderId\" FROM payment.payments WHERE \"CustomerId\" = @customerId AND \"IdempotencyKey\" = @key",
            ("customerId", McpHostFixture.TestCustomerId), ("key", idempotencyKey));
        paymentRows.Should().ContainSingle("bu key için TEK bir ödeme satırı olmalı")
            .Which.Should().Be(winningOrderId, "o satır KAZANAN siparişe ait olmalı");

        var activeReservations = await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM inventory.reservations WHERE \"ProductId\" = @productId AND \"Status\" = 'Active'",
            ("productId", productId));
        activeReservations.Should().Be(0, "yarıştan sonra Active'de asılı kalan (sızan) rezervasyon olmamalı");

        var committedReservations = await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM inventory.reservations WHERE \"ProductId\" = @productId AND \"Status\" = 'Committed'",
            ("productId", productId));
        committedReservations.Should().Be(1, "tam olarak KAZANANIN rezervasyonu kalıcı düşüşe çevrilmiş olmalı");

        var outboxCount = await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM ordering.outbox_messages WHERE \"Content\"::text LIKE @pattern",
            ("pattern", $"%{winningOrderId}%"));
        outboxCount.Should().Be(1, "kazanan sipariş için TAM 1 OrderPaid outbox mesajı olmalı, yetim/duplike yok");

        output.WriteLine($"Transport-seviyesi (ağ zaman aşımı vb., idempotency mantığıyla İLGİSİZ) retry sayısı: {_transportRetryCount}");
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
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
        // Transport-seviyesi (sunucuya hiç ulaşmadan client'ta oluşan — Polly'nin kendi 1s
        // per-attempt bütçesi, 100-paralel koşuda sistem yükü altında ara sıra aşılabiliyor)
        // hatalar İDEMPOTENCY mantığıyla İLGİSİZ test-ortamı gürültüsüdür; gerçek bir MCP
        // client'ın ağ zaman aşımında yapacağı AYNI şeyi (aynı key ile tekrar) yapıyoruz —
        // ayrı ve KÜÇÜK bir üst sınırla (domain-seviyesi 200 denemeden bilerek ayrı).
        const int maxTransportRetries = 5;
        var transportRetries = 0;

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
            catch (TimeoutRejectedException) when (transportRetries < maxTransportRetries)
            {
                transportRetries++;
                Interlocked.Increment(ref _transportRetryCount);
            }
            catch (HttpRequestException) when (transportRetries < maxTransportRetries)
            {
                transportRetries++;
                Interlocked.Increment(ref _transportRetryCount);
            }
            catch (TaskCanceledException ex) when (
                ex.CancellationToken != CancellationToken.None && transportRetries < maxTransportRetries)
            {
                // Testin KENDİ token'ından (hep CancellationToken.None geçiyoruz) gelmeyen bir
                // iptal — Polly'nin iç timeout mekanizmasının kendi CTS'i. Testin isteyerek
                // iptal ettiği bir işlem asla bu dala girmez (CancellationToken.None hiç
                // sinyal vermez).
                transportRetries++;
                Interlocked.Increment(ref _transportRetryCount);
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
