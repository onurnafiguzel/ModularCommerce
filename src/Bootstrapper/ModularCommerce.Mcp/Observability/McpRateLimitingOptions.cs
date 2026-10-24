namespace ModularCommerce.Mcp.Observability;

/// <summary>
/// MCP tool'ları için iki ayrı limit: okuma (search_products/check_stock/get_order_status/
/// get_cart) gevşek, yazma (set_cart_item/place_order) sıkı. Shared.Infrastructure'ın
/// AddRateLimiting'i HTTP-endpoint/route bazlı partition'lanıyor — MapMcp() TEK bir route'ta
/// tüm tool çağrılarını JSON-RPC ile multiplexler, yani route seviyesinde tool-bazlı ayrım
/// yapılamaz. Bu yüzden rate limiting burada ASP.NET Core middleware'i yerine ToolExecutionWrapper
/// içinde, programatik olarak (System.Threading.RateLimiting primitiflerini doğrudan kullanarak)
/// uygulanıyor — mantıksal "tool çağrısı" seviyesinde, HTTP route seviyesinde değil.
/// </summary>
public sealed class McpRateLimitingOptions
{
    public const string SectionName = "Mcp:RateLimiting";

    public RateLimitPolicyOptions Read { get; init; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitPolicyOptions Write { get; init; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; }
}
