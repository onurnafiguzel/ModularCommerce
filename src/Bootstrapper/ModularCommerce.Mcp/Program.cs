using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.AspNetCore;
using ModularCommerce.Mcp.Auth;
using ModularCommerce.Mcp.Clients;
using ModularCommerce.Shared.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<McpAuthDelegatingHandler>();

builder.Services
    .AddModularCommerceApiClient(builder.Configuration["ModularCommerceApi:BaseUrl"] ?? "http://localhost:8080")
    .AddHttpMessageHandler<McpAuthDelegatingHandler>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

await app.RunAsync();
