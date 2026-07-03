using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Shared.Infrastructure.Modules;

namespace ModularCommerce.Payment.Api;

public sealed class PaymentModule : IModule
{
    public string Name => "Payment";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Modülün servisleri, DbContext'i ve pipeline kayıtları buraya gelecek.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/payment");

        group.MapGet("/health", () => Results.Ok(new { module = "Payment", status = "healthy" }));
    }
}
