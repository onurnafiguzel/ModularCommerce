using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ModularCommerce.Mcp.Clients;

namespace ModularCommerce.Mcp.Tools;

/// <summary>ModularCommerce mağazasını bir AI agent'a açan MCP tool'ları.</summary>
[McpServerToolType]
public sealed class StoreTools(ModularCommerceApiClient apiClient)
{
    [McpServerTool, Description("Mağazada metin, kategori ve fiyat aralığına göre ürün arar, sayfalanmış sonuç döner.")]
    public Task<string> SearchProducts(
        [Description("Aranacak metin (ürün adı/açıklaması).")] string? query = null,
        [Description("Sayfa numarası, 1'den başlar.")] int page = 1,
        [Description("Sayfa başına ürün sayısı.")] int pageSize = 20,
        [Description("Minimum fiyat (dahil).")] decimal? minPrice = null,
        [Description("Maksimum fiyat (dahil).")] decimal? maxPrice = null,
        CancellationToken cancellationToken = default)
        => apiClient.SearchProductsAsync(query, page, pageSize, minPrice, maxPrice, cancellationToken);

    [McpServerTool, Description("Bir ürünün anlık stok durumunu kontrol eder.")]
    public async Task<string> CheckStock(
        [Description("Ürünün kimliği (GUID).")] string productId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(productId, out var id))
        {
            throw new McpException($"'{productId}' geçerli bir GUID değil.");
        }

        return await apiClient.CheckStockAsync(id, cancellationToken);
    }

    [McpServerTool, Description("Bir siparişin güncel durumunu sorgular.")]
    public async Task<string> GetOrderStatus(
        [Description("Siparişin kimliği (GUID).")] string orderId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(orderId, out var id))
        {
            throw new McpException($"'{orderId}' geçerli bir GUID değil.");
        }

        return await apiClient.GetOrderStatusAsync(id, cancellationToken);
    }

    [McpServerTool, Description(
        "Sepetteki ürünlerle sipariş oluşturur (checkout). idempotency_key'i kullanıcının her " +
        "yeni sipariş NİYETİ için BİR KEZ üret (örn. UUID). Aynı siparişi tekrar denerken (örn. " +
        "zaman aşımı sonrası) AYNI key'i kullan. Farklı bir sipariş için MUTLAKA yeni bir key " +
        "üret — aynı key'i farklı ürün/miktarla tekrar kullanırsan istek reddedilir (422).")]
    public async Task<string> PlaceOrder(
        [Description("Bu sipariş NİYETİ için benzersiz anahtar (UUID önerilir). Aynı niyetin " +
            "tekrar denemelerinde AYNI değer, yeni bir sipariş için YENİ bir değer kullanılmalı.")]
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await apiClient.PlaceOrderAsync(idempotencyKey, cancellationToken);
        }
        catch (McpException ex)
        {
            // place_order'a özgü ek rehberlik: retryable ise AYNI key, değilse YENİ key.
            throw new McpException(
                $"{ex.Message} Retryable ise bu ÇAĞRIYI AYNI idempotency_key ('{idempotencyKey}') " +
                "ile tekrar dene; değilse farklı bir sipariş için YENİ bir idempotency_key üret.");
        }
    }
}
