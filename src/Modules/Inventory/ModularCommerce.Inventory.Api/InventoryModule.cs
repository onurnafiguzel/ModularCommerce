using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Infrastructure.Modules;

namespace ModularCommerce.Inventory.Api;

public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Modülün servisleri, DbContext'i ve pipeline kayıtları buraya gelecek.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory");

        group.MapGet("/health", () => Results.Ok(new { module = "Inventory", status = "healthy" }));
    }
}
