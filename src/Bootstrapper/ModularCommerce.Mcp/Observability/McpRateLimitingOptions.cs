namespace ModularCommerce.Mcp.Observability;

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
