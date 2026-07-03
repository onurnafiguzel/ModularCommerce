using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Infrastructure.Modules;

namespace ModularCommerce.Shipping.Api;

public sealed class ShippingModule : IModule
{
    public string Name => "Shipping";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Modülün servisleri, DbContext'i ve pipeline kayıtları buraya gelecek.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/shipping");

        group.MapGet("/health", () => Results.Ok(new { module = "Shipping", status = "healthy" }));
    }
}
