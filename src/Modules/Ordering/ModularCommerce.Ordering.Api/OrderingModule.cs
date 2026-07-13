using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Ordering.Api.Endpoints;
using ModularCommerce.Ordering.Application.Abstractions;
using ModularCommerce.Ordering.Application.Orders.Cancel;
using ModularCommerce.Ordering.Application.Orders.Checkout;
using ModularCommerce.Ordering.Application.Orders.GetMyOrders;
using ModularCommerce.Ordering.Application.Orders.GetOrder;
using ModularCommerce.Ordering.Contracts;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.ContractAdapters;
using ModularCommerce.Ordering.Infrastructure.Outbox;
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
        // Outbox altyapısı: registry (singleton, durumsuz eşleme), interceptor (scoped,
        // DbContext ile aynı ömür). Interceptor YALNIZ OrderingDbContext'e bağlanır —
        // başka modülün DbContext'ine sızmaz (opt-in configure overload).
        services.AddSingleton<IIntegrationEventMapper, OrderingIntegrationEventRegistry>();
        services.AddScoped<DomainEventToOutboxInterceptor>();

        services.AddModuleDbContext<OrderingDbContext>(configuration, OrderingDbContext.Schema,
            configure: (serviceProvider, options) =>
                options.AddInterceptors(serviceProvider.GetRequiredService<DomainEventToOutboxInterceptor>()));

        services.AddHostedService<OutboxDispatcher>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        // TTL süpürücüsünün P2 reconcile sorgusu için (Inventory bu Contracts'ı çağırır).
        services.AddScoped<IOrderReservationReconciler, OrderReservationReconciler>();

        services.AddScoped<CheckoutHandler>();
        services.AddScoped<CancelOrderHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddScoped<GetMyOrdersHandler>();
        services.AddValidatorsFromAssemblyContaining<CheckoutCommandValidator>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ordering");

        group.MapOrderEndpoints();
    }
}
