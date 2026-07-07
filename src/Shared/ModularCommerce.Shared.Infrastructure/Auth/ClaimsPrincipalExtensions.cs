using System.Security.Claims;

namespace ModularCommerce.Shared.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");

        return Guid.TryParse(sub, out var userId)
            ? userId
            : throw new InvalidOperationException(
                "JWT 'sub' claim'i okunamadı — MapInboundClaims=false ayarını kontrol edin");
    }
}
