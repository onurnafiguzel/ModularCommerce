using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ModularCommerce.Shared.Infrastructure.Auth;
using Xunit;

namespace ModularCommerce.Shared.IntegrationTests;

/// <summary>
/// AddJwtAuthentication'ın çok-issuer desteği: MCP için AYRI bir issuer/signing key
/// tanımlandığında, issuer↔key doğru BAĞLANIYOR mu? Basit bir "IssuerSigningKeys listesi +
/// ValidIssuers listesi" yaklaşımı bunu garanti ETMEZ (imzayı listedeki herhangi bir key'e karşı
/// doğrular) — bu yüzden kritik negatif test: MCP key'iyle imzalanmış ama Host'un issuer'ını
/// iddia eden bir token KESİNLİKLE 401 almalı.
/// </summary>
public sealed class MultiIssuerJwtTests : IAsyncLifetime
{
    private const string HostIssuer = "test-host-issuer";
    private const string McpIssuer = "test-mcp-issuer";
    private const string Audience = "test-audience";
    private const string HostSigningKey = "test-host-signing-key-at-least-32-characters!!";
    private const string McpSigningKey = "test-mcp-signing-key-totally-different-value!!!";

    private IHost? _host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = HostIssuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = HostSigningKey,
                ["Jwt:Mcp:Issuer"] = McpIssuer,
                ["Jwt:Mcp:SigningKey"] = McpSigningKey,
            })
            .Build();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddJwtAuthentication(builder.Configuration);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/whoami", (ClaimsPrincipal user) => Results.Ok(user.FindFirst("sub")?.Value))
            .RequireAuthorization();

        await app.StartAsync();
        _host = app;

        var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private static string CreateToken(string issuer, string signingKey)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: Audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact(DisplayName = "Host'un kendi issuer+key'iyle imzalanmış token kabul edilir (200)")]
    public async Task HostToken_IsAccepted()
    {
        var token = CreateToken(HostIssuer, HostSigningKey);

        var response = await SendWithToken(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "MCP'nin kendi issuer+key'iyle imzalanmış token kabul edilir (200) — çok-issuer desteği çalışıyor")]
    public async Task McpToken_WithMatchingIssuer_IsAccepted()
    {
        var token = CreateToken(McpIssuer, McpSigningKey);

        var response = await SendWithToken(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "KRİTİK: MCP key'iyle imzalanmış ama Host'un issuer'ını iddia eden token REDDEDİLİR (401) — issuer↔key bağlı doğrulama")]
    public async Task McpKeySignedToken_ClaimingHostIssuer_IsRejected()
    {
        // Basit bir IssuerSigningKeys+ValidIssuers listesi bu tokenı YANLIŞLIKLA kabul ederdi
        // (imza listedeki herhangi bir key'e karşı doğrulanır) — IssuerSigningKeyResolver'ın
        // issuer'a göre SADECE o issuer'a ait key döndürmesi bunu engellemeli.
        var forgedToken = CreateToken(HostIssuer, McpSigningKey);

        var response = await SendWithToken(forgedToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> SendWithToken(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
