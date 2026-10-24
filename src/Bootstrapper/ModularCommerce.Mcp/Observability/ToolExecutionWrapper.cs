using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace ModularCommerce.Mcp.Observability;

/// <summary>
/// Her tool çağrısının TEK giriş noktası: rate limit kontrolü + span (tool adı, süre, sonuç,
/// idempotency key) + yapılandırılmış log — 4+ tool metodunda kopya kod yok, StoreTools sadece
/// bu sarmalayıcıyı çağırır.
/// </summary>
public sealed class ToolExecutionWrapper(
    ILogger<ToolExecutionWrapper> logger,
    IHttpContextAccessor httpContextAccessor,
    McpRateLimiter rateLimiter)
{
    public static readonly ActivitySource ActivitySource = new("ModularCommerce.Mcp");

    public async Task<T> ExecuteAsync<T>(
        string toolName,
        ToolRateLimitPolicy policy,
        Func<Task<T>> operation,
        string? idempotencyKey = null)
    {
        var customerId = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value ?? "unknown";

        if (!rateLimiter.TryAcquire(policy, customerId, out var retryAfter))
        {
            logger.LogWarning(
                "MCP tool rate-limited: {ToolName} (customer {CustomerId}, retryAfter={RetryAfter})",
                toolName, customerId, retryAfter);

            throw new McpException(
                $"'{toolName}' için çok fazla istek gönderildi; " +
                $"{Math.Ceiling(retryAfter.TotalSeconds)} saniye sonra tekrar deneyebilirsin.");
        }

        using var activity = ActivitySource.StartActivity(toolName, ActivityKind.Client);
        activity?.SetTag("tool.name", toolName);
        activity?.SetTag("tool.customer_id", customerId);
        if (idempotencyKey is not null)
        {
            activity?.SetTag("tool.idempotency_key", idempotencyKey);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await operation();

            activity?.SetTag("tool.outcome", "success");
            logger.LogInformation(
                "MCP tool çağrısı tamamlandı: {ToolName} ({DurationMs}ms) outcome={Outcome}",
                toolName, stopwatch.ElapsedMilliseconds, "success");

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("tool.outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(
                "MCP tool çağrısı hata ile sonuçlandı: {ToolName} ({DurationMs}ms) outcome={Outcome}: {ErrorMessage}",
                toolName, stopwatch.ElapsedMilliseconds, "error", ex.Message);

            throw;
        }
    }
}
