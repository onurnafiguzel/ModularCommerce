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

builder.Services.AddProblemDetails();

builder.Services.AddRateLimiting(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<McpAuthDelegatingHandler>();

builder.Services
    .AddModularCommerceApiClient(builder.Configuration["ModularCommerceApi:BaseUrl"] ?? "http://localhost:8080")
    .AddHttpMessageHandler<McpAuthDelegatingHandler>();

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

// "/mcp" sabit: k8s Ingress bu path'i Mcp Service'ine rewrite'sız yönlendirir (ingress.yaml) —
// pattern burada değişirse ingress path'i de güncellenmeli.
app.MapMcp("/mcp").RequireAuthorization();

await app.RunAsync();
