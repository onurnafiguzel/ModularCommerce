using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Identity.Application.Auth.Login;
using ModularCommerce.Identity.Application.Auth.Logout;
using ModularCommerce.Identity.Application.Auth.Refresh;
using ModularCommerce.Identity.Application.Auth.Signup;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Identity.Api.Endpoints;

public sealed record LogoutRequest(string RefreshToken);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("/signup", async (
            SignupCommand command,
            SignupHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            // GET /users/{id} olmadığı için Location bilinçli olarak verilmez.
            return result.IsSuccess
                ? Results.Created((string?)null, result.Value)
                : result.ToHttpResult();
        });

        group.MapPost("/login", async (
            LoginCommand command,
            LoginHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.ToHttpResult();
        });

        group.MapPost("/refresh", async (
            RefreshCommand command,
            RefreshHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.ToHttpResult();
        });

        // Yalnız token sahibi kendi oturumunu kapatır: UserId JWT'nin sub claim'inden.
        group.MapPost("/logout", async (
            LogoutRequest request,
            ClaimsPrincipal user,
            LogoutHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new LogoutCommand(request.RefreshToken, user.GetUserId());
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
