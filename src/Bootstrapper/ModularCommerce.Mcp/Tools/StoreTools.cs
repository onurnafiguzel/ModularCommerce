using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ModularCommerce.Mcp.Clients;
using ModularCommerce.Mcp.Observability;

namespace ModularCommerce.Mcp.Tools;

/// <summary>ModularCommerce mağazasını bir AI agent'a açan MCP tool'ları.</summary>
[McpServerToolType]
public sealed class StoreTools(ModularCommerceApiClient apiClient, ToolExecutionWrapper execution)
{
    [McpServerTool, Description("Mağazada metin, kategori ve fiyat aralığına göre ürün arar, sayfalanmış sonuç döner.")]
    public Task<string> SearchProducts(
        [Description("Aranacak metin (ürün adı/açıklaması).")] string? query = null,
        [Description("Sayfa numarası, 1'den başlar.")] int page = 1,
        [Description("Sayfa başına ürün sayısı.")] int pageSize = 20,
        [Description("Minimum fiyat (dahil).")] decimal? minPrice = null,
        [Description("Maksimum fiyat (dahil).")] decimal? maxPrice = null,
        CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(
            "search_products",
            ToolRateLimitPolicy.Read,
            () => apiClient.SearchProductsAsync(query, page, pageSize, minPrice, maxPrice, cancellationToken));

    [McpServerTool, Description("Bir ürünün anlık stok durumunu kontrol eder.")]
    public Task<string> CheckStock(
        [Description("Ürünün kimliği (GUID).")] string productId,
        CancellationToken cancellationToken = default)
        => execution.ExecuteAsync("check_stock", ToolRateLimitPolicy.Read, () =>
        {
            if (!Guid.TryParse(productId, out var id))
            {
                throw new McpException($"'{productId}' geçerli bir GUID değil.");
            }

            return apiClient.CheckStockAsync(id, cancellationToken);
        });

    [McpServerTool, Description("Mevcut sepetin içeriğini (ürünler ve miktarları) döner.")]
    public Task<string> GetCart(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync("get_cart", ToolRateLimitPolicy.Read, () => apiClient.GetCartAsync(cancellationToken));

    [McpServerTool, Description(
        "Sepetteki bir ürünün miktarını TAM OLARAK belirtilen değere ayarlar — 'ekle/artır' DEĞİL, " +
        "'miktarı X yap' semantiği. quantity=0 verilirse ürün sepetten kaldırılır. İDEMPOTENTTİR: " +
        "aynı çağrıyı art arda tekrarlasan bile sepet bozulmaz/çoğalmaz (altta yatan 'ürün ekle' " +
        "API'si artırmalı çalışır, ama bu tool önce sepeti okuyup ürünün sepette olup olmadığına " +
        "bakar ve sadece ürün sepette HİÇ YOKKEN o artırmalı uca gider — o durumda sıfırdan " +
        "başladığı için sonuç yine de 'tam olarak istenen miktar' olur).")]
    public Task<string> SetCartItem(
        [Description("Ürünün kimliği (GUID).")] string productId,
        [Description("Hedef miktar. 0 → ürün sepetten kaldırılır. 1+ → sepetteki miktar TAM OLARAK bu değere ayarlanır (mevcut miktara eklenmez).")]
        int quantity,
        CancellationToken cancellationToken = default)
        => execution.ExecuteAsync("set_cart_item", ToolRateLimitPolicy.Write, () =>
        {
            if (!Guid.TryParse(productId, out var id))
            {
                throw new McpException($"'{productId}' geçerli bir GUID değil.");
            }

            if (quantity < 0)
            {
                throw new McpException("quantity negatif olamaz (0 = kaldır, 1+ = hedef miktar).");
            }

            return apiClient.SetCartItemAsync(id, quantity, cancellationToken);
        });

    [McpServerTool, Description("Bir siparişin güncel durumunu sorgular.")]
    public Task<string> GetOrderStatus(
        [Description("Siparişin kimliği (GUID).")] string orderId,
        CancellationToken cancellationToken = default)
        => execution.ExecuteAsync("get_order_status", ToolRateLimitPolicy.Read, () =>
        {
            if (!Guid.TryParse(orderId, out var id))
            {
                throw new McpException($"'{orderId}' geçerli bir GUID değil.");
            }

            return apiClient.GetOrderStatusAsync(id, cancellationToken);
        });

    [McpServerTool, Description(
        "Sepetteki ürünlerle sipariş oluşturur (checkout). idempotency_key'i kullanıcının her " +
        "yeni sipariş NİYETİ için BİR KEZ üret (örn. UUID). Aynı siparişi tekrar denerken (örn. " +
        "zaman aşımı sonrası) AYNI key'i kullan. Farklı bir sipariş için MUTLAKA yeni bir key " +
        "üret — aynı key'i farklı ürün/miktarla tekrar kullanırsan istek reddedilir (422).")]
    public Task<string> PlaceOrder(
        [Description("Bu sipariş NİYETİ için benzersiz anahtar (UUID önerilir). Aynı niyetin " +
            "tekrar denemelerinde AYNI değer, yeni bir sipariş için YENİ bir değer kullanılmalı.")]
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(
            "place_order",
            ToolRateLimitPolicy.Write,
            async () =>
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
            },
            idempotencyKey);
}
