using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Infrastructure.Modules;

namespace ModularCommerce.Cart.Api;

public sealed class CartModule : IModule
{
    public string Name => "Cart";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Modülün servisleri, DbContext'i ve pipeline kayıtları buraya gelecek.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cart");

        group.MapGet("/health", () => Results.Ok(new { module = "Cart", status = "healthy" }));
    }
}
