using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace ModularCommerce.Mcp.Observability;

public enum ToolRateLimitPolicy
{
    Read,
    Write,
}

/// <summary>
/// CustomerId'ye (X-Api-Key'den çözülen) göre partition'lanmış, iki ayrı sliding-window limiter —
/// biri okuma tool'ları için gevşek, biri yazma tool'ları için sıkı.
/// </summary>
public sealed class McpRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _read;
    private readonly PartitionedRateLimiter<string> _write;

    public McpRateLimiter(IOptions<McpRateLimitingOptions> options)
    {
        _read = BuildLimiter(options.Value.Read);
        _write = BuildLimiter(options.Value.Write);
    }

    public bool TryAcquire(ToolRateLimitPolicy policy, string partitionKey)
    {
        var limiter = policy == ToolRateLimitPolicy.Read ? _read : _write;
        using var lease = limiter.AttemptAcquire(partitionKey);
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<string> BuildLimiter(RateLimitPolicyOptions options)
        => PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
            }));

    public void Dispose()
    {
        _read.Dispose();
        _write.Dispose();
    }
}
