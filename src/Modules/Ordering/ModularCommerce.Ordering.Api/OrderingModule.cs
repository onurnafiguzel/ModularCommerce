using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Infrastructure.Modules;

namespace ModularCommerce.Ordering.Api;

public sealed class OrderingModule : IModule
{
    public string Name => "Ordering";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Modülün servisleri, DbContext'i ve pipeline kayıtları buraya gelecek.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ordering");

        group.MapGet("/health", () => Results.Ok(new { module = "Ordering", status = "healthy" }));
    }
}
