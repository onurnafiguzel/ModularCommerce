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
/// <remarks>
/// BİLİNEN SINIRLAMA: limiter'lar process-in-memory'dir (paylaşılan bir store — Redis vb. — YOK).
/// k8s'te 3 replika çalışırken her pod kendi sayacını tutar; aynı müşteri farklı pod'lara
/// yönlendirilirse efektif limit PermitLimit × replika sayısı kadar gevşer (bkz. README "Bilinen
/// Sınırlamalar"). Düzeltme: Redis tabanlı (ör. StackExchange.Redis + Lua script) paylaşılan
/// bir limiter — bugünkü kapsamda değil.
/// </remarks>
public sealed class McpRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _read;
    private readonly PartitionedRateLimiter<string> _write;
    private readonly TimeSpan _readWindow;
    private readonly TimeSpan _writeWindow;

    public McpRateLimiter(IOptions<McpRateLimitingOptions> options)
    {
        _read = BuildLimiter(options.Value.Read);
        _write = BuildLimiter(options.Value.Write);
        _readWindow = TimeSpan.FromSeconds(options.Value.Read.WindowSeconds);
        _writeWindow = TimeSpan.FromSeconds(options.Value.Write.WindowSeconds);
    }

    /// <summary>
    /// Lease alınamazsa (limit aşıldıysa) <paramref name="retryAfter"/>'a "kaç süre sonra tekrar
    /// dene" değerini koyar — çağıran (ToolExecutionWrapper) bunu McpException mesajına somut bir
    /// süre olarak yansıtır. DOĞRULANMIŞ: SlidingWindowRateLimiter'ın reddedilen lease'i
    /// MetadataNames'te "RETRY_AFTER" listeler ama TryGetMetadata(MetadataName.RetryAfter, ...)
    /// yine de false döner (elle test edildi — FixedWindow/TokenBucket'ta sorunsuz çalışıyor,
    /// yalnız SlidingWindow'da bu kopukluk var) — bu yüzden metadata gelmezse policy'nin
    /// WindowSeconds'ı güvenli üst-sınır fallback olarak kullanılır (Shared.Infrastructure/
    /// RateLimiting'deki OnRejectedAsync'in AYNI, önceden doğrulanmış deseni).
    /// </summary>
    public bool TryAcquire(ToolRateLimitPolicy policy, string partitionKey, out TimeSpan retryAfter)
    {
        var (limiter, fallbackWindow) = policy == ToolRateLimitPolicy.Read
            ? (_read, _readWindow)
            : (_write, _writeWindow);

        using var lease = limiter.AttemptAcquire(partitionKey);

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var metadataValue)
            ? metadataValue
            : fallbackWindow;

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
