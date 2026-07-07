using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Ordering.Api.Endpoints;
using ModularCommerce.Ordering.Application.Abstractions;
using ModularCommerce.Ordering.Application.Orders.Checkout;
using ModularCommerce.Ordering.Application.Orders.GetMyOrders;
using ModularCommerce.Ordering.Application.Orders.GetOrder;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.Persistence;
using ModularCommerce.Ordering.Infrastructure.Persistence.Queries;
using ModularCommerce.Ordering.Infrastructure.Persistence.Repositories;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Ordering.Api;

public sealed class OrderingModule : IModule
{
    public string Name => "Ordering";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrderingDbContext>(configuration, OrderingDbContext.Schema);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        services.AddScoped<CheckoutHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddScoped<GetMyOrdersHandler>();
        services.AddValidatorsFromAssemblyContaining<CheckoutCommandValidator>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ordering");

        group.MapGet("/health", () => Results.Ok(new { module = "Ordering", status = "healthy" }));
        group.MapOrderEndpoints();
    }
}
