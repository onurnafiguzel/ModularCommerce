using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ModularCommerce.Mcp.Tools;

/// <summary>
/// ModularCommerce mağazasını bir AI agent'a açan MCP tool'ları. İskelet aşaması — gerçek HTTP
/// çağrıları ve hata eşlemesi sonraki adımda eklenecek (bkz. plan Faz 1 Adım 3).
/// </summary>
[McpServerToolType]
public static class StoreTools
{
    [McpServerTool, Description("Mağazada metin, kategori ve fiyat aralığına göre ürün arar, sayfalanmış sonuç döner.")]
    public static string SearchProducts(
        [Description("Aranacak metin (ürün adı/açıklaması).")] string? query = null,
        [Description("Sayfa numarası, 1'den başlar.")] int page = 1,
        [Description("Sayfa başına ürün sayısı.")] int pageSize = 20,
        [Description("Minimum fiyat (dahil).")] decimal? minPrice = null,
        [Description("Maksimum fiyat (dahil).")] decimal? maxPrice = null)
        => "TODO: GET /api/catalog/products";

    [McpServerTool, Description("Bir ürünün anlık stok durumunu kontrol eder.")]
    public static string CheckStock(
        [Description("Ürünün kimliği (GUID).")] string productId)
        => "TODO: GET /api/inventory/stock/{productId}";

    [McpServerTool, Description("Bir siparişin güncel durumunu sorgular.")]
    public static string GetOrderStatus(
        [Description("Siparişin kimliği (GUID).")] string orderId)
        => "TODO: GET /api/ordering/orders/{orderId}";

    [McpServerTool, Description(
        "Sepetteki ürünlerle sipariş oluşturur (checkout). idempotency_key'i kullanıcının her " +
        "yeni sipariş NİYETİ için BİR KEZ üret (örn. UUID). Aynı siparişi tekrar denerken (örn. " +
        "zaman aşımı sonrası) AYNI key'i kullan. Farklı bir sipariş için MUTLAKA yeni bir key " +
        "üret — aynı key'i farklı ürün/miktarla tekrar kullanırsan istek reddedilir (422).")]
    public static string PlaceOrder(
        [Description("Bu sipariş NİYETİ için benzersiz anahtar (UUID önerilir). Aynı niyetin " +
            "tekrar denemelerinde AYNI değer, yeni bir sipariş için YENİ bir değer kullanılmalı.")]
        string idempotencyKey)
        => "TODO: POST /api/ordering/checkout";
}
