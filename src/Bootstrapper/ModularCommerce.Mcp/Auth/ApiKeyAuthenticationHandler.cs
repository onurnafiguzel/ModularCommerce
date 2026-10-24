using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularCommerce.Mcp.Auth;

/// <summary>
/// MCP sunucusunun kendi girişini korur: X-Api-Key header'ını <see cref="IApiKeyStore"/> ile
/// bir CustomerId'ye çözer, başarılıysa "sub" claim'inde o CustomerId'yi taşıyan bir principal
/// üretir. Bu principal downstream Host API çağrıları için JWT basmakta kullanılır
/// (bkz. IServiceJwtIssuer) — MCP'nin KENDİ auth'u ile Host'a giden JWT birbirinden farklı
/// mekanizmalardır.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyStore apiKeyStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.Fail($"'{HeaderName}' header eksik."));
        }

        var customerId = apiKeyStore.ResolveCustomerId(values.ToString());
        if (customerId is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Geçersiz API key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim("sub", customerId.Value.ToString())], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
