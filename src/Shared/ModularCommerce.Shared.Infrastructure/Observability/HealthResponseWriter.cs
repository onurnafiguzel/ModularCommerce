using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ModularCommerce.Shared.Infrastructure.Observability;

/// <summary>
/// Health raporunu tutarlı bir JSON gövdesine çevirir (SRP — kontrol mantığından ayrı): genel
/// durum + prob başına ad/durum/süre/hata. Ops ve K6 bu gövdeden hangi bağımlılığın düştüğünü okur.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                error = entry.Value.Exception?.Message,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload, JsonOptions);
    }
}
