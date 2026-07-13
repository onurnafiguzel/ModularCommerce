using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Shared.Infrastructure.RateLimiting;
public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        Validate(options);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global catch-all: kimlikliyse kullanıcı, değilse IP bazlı sliding window (D3).
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    UserOrIpKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = options.Global.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.Global.WindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                    }));

            // "auth": login/signup, IP bazlı, sıkı → tek IP'den brute-force/flood 429'lanır (D4).
            limiter.AddPolicy("auth", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    IpKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = options.Auth.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.Auth.WindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                    }));

            // "checkout": kullanıcı bazlı, permit+queue ile burst-absorbing → §10.2'nin 100-paralel
            // aynı-key burst'ü kuyruğa alınıp geçer; yalnız SÜREKLİ tek-kullanıcı kötüye kullanımı kesilir (D5).
            limiter.AddPolicy("checkout", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    UserOrIpKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = options.Checkout.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.Checkout.WindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = options.Checkout.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }));

            // 429 + Retry-After + tutarlı ProblemDetails (GlobalExceptionHandler simetriği).
            // options'ı yakalar: limiter RetryAfter metadata'sı vermezse pencere kadar hint yazılır
            // (istemci kontratı Retry-After'ın HER 429'da bulunmasına dayanır).
            limiter.OnRejected = (context, cancellationToken)
                => OnRejectedAsync(context, options, cancellationToken);
        });

        return services;
    }

    private static async ValueTask OnRejectedAsync(
        OnRejectedContext context,
        RateLimitingOptions options,
        CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : options.Global.WindowSeconds; // metadata yoksa güvenli üst-sınır hint
        http.Response.Headers.RetryAfter =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        // Endpoint hata yoluyla AYNI kabuk: IProblemDetailsService (traceId + correlationId
        // CustomizeProblemDetails'ten) + code + retryable. 429 doğası gereği retryable'dır
        // (Retry-After kadar bekle, sonra tekrar) — sözleşme yapısal olarak yazılır.
        var problem = new ProblemDetails
        {
            Title = "RateLimited",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "İstek hız sınırı aşıldı; Retry-After başlığı kadar bekleyip aynı istekle tekrar deneyin.",
        };
        problem.Extensions[ProblemMapping.CodeExtension] = "RateLimited";
        problem.Extensions[ProblemMapping.RetryableExtension] = true;

        var problemDetailsService = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
        });
    }

    // Partition anahtarları: 'sub' claim'i (MapInboundClaims=false) varsa kullanıcı, yoksa IP.
    // GetUserId() anonimde fırlatır — burada güvenli okuma gerekir.
    private static string UserOrIpKey(HttpContext context)
        => context.User.FindFirst("sub")?.Value ?? IpKey(context);

    private static string IpKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static void Validate(RateLimitingOptions options)
    {
        if (options.Global.PermitLimit <= 0 || options.Global.WindowSeconds <= 0
            || options.Auth.PermitLimit <= 0 || options.Auth.WindowSeconds <= 0
            || options.Checkout.PermitLimit <= 0 || options.Checkout.WindowSeconds <= 0
            || options.Checkout.QueueLimit < 0)
        {
            throw new InvalidOperationException(
                "RateLimiting: PermitLimit/WindowSeconds pozitif, QueueLimit negatif olmamalı.");
        }

        if (options.Checkout.PermitLimit + options.Checkout.QueueLimit < 100)
        {
            throw new InvalidOperationException(
                "RateLimiting:Checkout PermitLimit + QueueLimit ≥ 100 olmalı (checkout-smoke §10.2 idempotency kanıtı).");
        }
    }
}
