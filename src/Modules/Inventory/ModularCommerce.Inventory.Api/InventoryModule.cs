using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularCommerce.Inventory.Api.Endpoints;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Application.Reservations.GetReservation;
using ModularCommerce.Inventory.Application.Reservations.ReserveStock;
using ModularCommerce.Inventory.Application.Stock.GetStock;
using ModularCommerce.Inventory.Application.Stock.SetStock;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.BackgroundJobs;
using ModularCommerce.Inventory.Infrastructure.ContractAdapters;
using ModularCommerce.Inventory.Infrastructure.Locking;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.Inventory.Infrastructure.Persistence.Queries;
using ModularCommerce.Inventory.Infrastructure.Persistence.Repositories;
using ModularCommerce.Inventory.Infrastructure.Persistence.Seed;
using ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Inventory.Api;

public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<InventoryDbContext>(configuration, InventoryDbContext.Schema);

        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();

        services.AddScoped<IStockReservationService, StockReservationService>();
        services.AddScoped<IDataSeeder<InventoryDbContext>, InventoryDataSeeder>();
       
        var strategyName = configuration["Inventory:ReservationStrategy"]
                                              ?? "OptimisticConcurrency";

        switch (strategyName)
        {
            case "Naive":
                services.AddScoped<IReservationStrategy, NaiveReservationStrategy>();
                break;
            case "OptimisticConcurrency":
                services.AddScoped<IReservationStrategy, OptimisticConcurrencyReservationStrategy>();
                break;
            case "RedisLock":
                services.AddScoped<IDistributedLock, RedisDistributedLock>();
                services.AddScoped<IReservationStrategy, RedisLockReservationStrategy>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Bilinmeyen rezervasyon stratejisi: '{strategyName}'. " +
                    "Geçerli değerler: Naive, OptimisticConcurrency, RedisLock.");
        }

        services.AddScoped<ReserveStockHandler>();
        services.AddScoped<GetReservationHandler>();
        services.AddScoped<GetStockHandler>();
        services.AddScoped<SetStockHandler>();
        services.AddValidatorsFromAssemblyContaining<ReserveStockCommandValidator>(includeInternalTypes: true);

        // Rezervasyon TTL süpürücüsü (W9): süresi dolmuş sahipsiz rezervasyonları iade eder,
        // Paid siparişe bağlı olanları Ordering'e sorarak Commit'e çevirir (P2 reconcile).
        services.AddHostedService<ReservationTtlSweeper>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory");
        var isDevelopment = endpoints.ServiceProvider
            .GetRequiredService<IHostEnvironment>()
            .IsDevelopment();

        group.MapReservationEndpoints();
        group.MapStockEndpoints(isDevelopment);
    }
}
