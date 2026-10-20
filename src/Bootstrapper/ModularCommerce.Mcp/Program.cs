using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.AspNetCore;
using ModularCommerce.Mcp.Auth;
using ModularCommerce.Mcp.Clients;
using ModularCommerce.Mcp.Observability;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.Observability;
using ModularCommerce.Shared.Infrastructure.RateLimiting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration).Enrich.With<ActivityEnricher>());

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "modularcommerce-mcp",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown")
        .AddAttributes([new("deployment.environment", builder.Environment.EnvironmentName)]))
    .WithTracing(tracing => tracing
        .AddSource(ToolExecutionWrapper.ActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IApiKeyStore, ConfigApiKeyStore>();
builder.Services.AddSingleton<IServiceJwtIssuer, McpServiceJwtIssuer>();

// MCP'nin KENDİ girişi: X-Api-Key → CustomerId (ApiKeyAuthenticationHandler). Host'a giden
// JWT bambaşka bir mekanizma (IServiceJwtIssuer, ayrı issuer/key) — ikisi karıştırılmamalı.
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// AddRateLimiting'in OnRejected yolu IProblemDetailsService kullanıyor (Host'taki gibi).
builder.Services.AddProblemDetails();

// DIŞTAN, KABA, IP bazlı HTTP katmanı: Shared.Infrastructure/RateLimiting'in AYNI GlobalLimiter'ı
// (auth'tan ÖNCE çalışır — UseRateLimiter, UseAuthentication'dan önce — context.User henüz
// kimliksiz olduğu için UserOrIpKey her zaman IP'ye düşer). Amaç: auth handler'a hiç ulaşmadan
// kaba bir istek selini kesmek. Tool-bazlı (Read/Write) ince ayrım İÇERDE, ToolExecutionWrapper'da.
builder.Services.AddRateLimiting(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<McpAuthDelegatingHandler>();

builder.Services
    .AddModularCommerceApiClient(builder.Configuration["ModularCommerceApi:BaseUrl"] ?? "http://localhost:8080")
    .AddHttpMessageHandler<McpAuthDelegatingHandler>();

// Tool-seviyeli rate limiting + span/log — bkz. McpRateLimitingOptions üzerindeki not: MapMcp()
// tek bir route'ta tüm tool'ları JSON-RPC ile multiplexlediği için HTTP-endpoint bazlı
// Shared.Infrastructure/RateLimiting (yukarıdaki GlobalLimiter dışında) tool-bazlı ayrım için
// kullanılamıyor, programatik bir alternatif var.
builder.Services.Configure<McpRateLimitingOptions>(
    builder.Configuration.GetSection(McpRateLimitingOptions.SectionName));
builder.Services.AddSingleton<McpRateLimiter>();
builder.Services.AddSingleton<ToolExecutionWrapper>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

await app.RunAsync();
