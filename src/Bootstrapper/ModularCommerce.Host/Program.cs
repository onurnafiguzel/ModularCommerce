using System.Diagnostics;
using MassTransit;
using ModularCommerce.Discovery.Api.Consumers;
using ModularCommerce.Notification.Api.Consumers;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.ExceptionHandling;
using ModularCommerce.Shared.Infrastructure.Messaging;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Observability;
using ModularCommerce.Shared.Infrastructure.RateLimiting;
using ModularCommerce.Shared.Infrastructure.Redis;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration).Enrich.With<ActivityEnricher>());

builder.Services.AddRedis(builder.Configuration);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "modularcommerce-api",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown")
        .AddAttributes([new("deployment.environment", builder.Environment.EnvironmentName)]))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health")
                && !httpContext.Request.Path.StartsWithSegments("/metrics"))
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("MassTransit")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("MassTransit")
        .AddPrometheusExporter());
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

        if (context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }

        if (context.Exception is not null &&
            context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            context.ProblemDetails.Extensions["exception"] = context.Exception.ToString();
        }
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddDependencyHealthChecks();

IModule[] modules =
[
    new ModularCommerce.Identity.Api.IdentityModule(),
    new ModularCommerce.Catalog.Api.CatalogModule(),
    new ModularCommerce.Cart.Api.CartModule(),
    new ModularCommerce.Inventory.Api.InventoryModule(),
    new ModularCommerce.Ordering.Api.OrderingModule(),
    new ModularCommerce.Payment.Api.PaymentModule(),
    new ModularCommerce.Shipping.Api.ShippingModule(),
    new ModularCommerce.Notification.Api.NotificationModule(),
    new ModularCommerce.Discovery.Api.DiscoveryModule(),
];

// AddEventBus, modüllerin Register'ından ÖNCE: MassTransit'in bus hosted service'i, modüllerin outbox
// dispatcher'larından (ve migrate+seed'den) ÖNCE başlar → tüketici kuyruk binding'leri hazır olur.
// Aksi halde seed anında (startup) publish edilen event'ler henüz bağlanmamış exchange'e gidip DÜŞER
// (OrderPaid bunu görmez çünkü yalnız runtime'da yayınlanır; Catalog ise seed'de yayınlar).
builder.Services.AddEventBus(builder.Configuration, consumers =>
{
    consumers.AddConsumer<OrderPaidNotificationConsumer, OrderPaidNotificationConsumerDefinition>();
    consumers.AddConsumer<ProductChangedConsumer, ProductChangedConsumerDefinition>();
});

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
    options.GetLevel = (httpContext, _, exception) =>
        exception is not null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/health") ||
              httpContext.Request.Path.StartsWithSegments("/metrics")
                ? LogEventLevel.Verbose   // minimum seviye Information → bu satırlar yazılmaz
                : LogEventLevel.Information);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthEndpoints();

// /metrics'i ayrı bir portta (varsayılan 9464) sunuyoruz ki Service/Ingress sadece 8080'i açınca
// dışarıdan erişilemez olsun; Prometheus pod'a doğrudan bu porttan (ClusterIP üzerinden, Service'siz)
// ulaşır. Port sabit yazılmadı, appsettings/env ile değiştirilebilir (Observability:MetricsPort).
var metricsPort = app.Configuration.GetValue("Observability:MetricsPort", 9464);
app.MapPrometheusScrapingEndpoint().RequireHost($"*:{metricsPort}").DisableRateLimiting();

app.MapGet("/", () => Results.Ok(new
{
    application = "ModularCommerce",
    modules = modules.Select(m => m.Name),
})).DisableRateLimiting();

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

// RUN_MIGRATIONS=true → k8s migration Job modu: hosted service'ler (migrate+seed dahil) çalışsın,
// sonra süreç temiz kapansın. HTTP dinlemeye hiç girmiyoruz; StopApplication tek tek çağrılmıyor
// çünkü her modülün kendi MigrateAndSeedHostedService instance'ı var, ilk biten diğerlerini keser.
if (app.Configuration.GetValue<bool>("RUN_MIGRATIONS"))
{
    await app.StartAsync();
    await app.StopAsync();
    return;
}

await app.RunAsync();

// WebApplicationFactory<Program> şartı: top-level statement Program.cs'in örtük Program sınıfını
// test projelerinin görebilmesi için public partial olması gerekir. Davranışsal etkisi yok.
public partial class Program;
