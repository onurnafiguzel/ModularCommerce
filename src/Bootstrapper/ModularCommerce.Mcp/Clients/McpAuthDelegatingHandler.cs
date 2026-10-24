using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using ModularCommerce.Mcp.Auth;

namespace ModularCommerce.Mcp.Clients;

/// <summary>
/// Her istek öncesi, o anki MCP çağrısının kimliğini doğrulayan CustomerId (X-Api-Key'den gelen,
/// bkz. ApiKeyAuthenticationHandler) için TAZE bir JWT basar ve Authorization header'ına koyar.
/// Token request-scoped'tur — global/statik DEĞİL, her HTTP çağrısında yeniden basılır.
/// </summary>
public sealed class McpAuthDelegatingHandler(
    IHttpContextAccessor httpContextAccessor,
    IServiceJwtIssuer jwtIssuer) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var customerIdClaim = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
        if (customerIdClaim is not null && Guid.TryParse(customerIdClaim, out var customerId))
        {
            var token = jwtIssuer.IssueToken(customerId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
