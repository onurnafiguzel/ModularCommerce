using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Catalog.Api.Endpoints;
using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Products.GetProductById;
using ModularCommerce.Catalog.Application.Products.GetProducts;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Catalog.Contracts;
using ModularCommerce.Catalog.Infrastructure.Caching;
using ModularCommerce.Catalog.Infrastructure.Persistence;
using ModularCommerce.Catalog.Infrastructure.Persistence.Queries;
using ModularCommerce.Catalog.Infrastructure.Persistence.Repositories;
using ModularCommerce.Catalog.Infrastructure.Persistence.Seed;
using ModularCommerce.Shared.Infrastructure.Configuration;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Catalog.Api;

public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CatalogDbContext>(configuration, CatalogDbContext.Schema);

        services.AddScoped<IProductRepository, ProductRepository>();

        // Okuma yolları read-through cache ile KOŞULSUZ decorate edilir (Decorator, OCP). Cache bir
        // optimizasyondur: RedisProductCache graceful degrade eder (Redis düşerse DB'ye düşülür) ve
        // Catalog:Cache:Enabled=false iken saf passthrough olur → modül dallanmasız kalır.
        services.AddValidatedOptions<CatalogCacheOptions>(configuration, CatalogCacheOptions.SectionName);
        services.AddSingleton<IProductCache, RedisProductCache>();

        services.AddScoped<ProductQueries>();
        services.AddScoped<IProductQueries>(
            sp => new CachingProductQueries(
            sp.GetRequiredService<ProductQueries>(), 
            sp.GetRequiredService<IProductCache>()
            ));

        services.AddScoped<ProductReader>();
        services.AddScoped<IProductReader>(
            sp => new CachingProductReader(
            sp.GetRequiredService<ProductReader>(),
            sp.GetRequiredService<IProductCache>()
            ));

        services.AddScoped<IDataSeeder<CatalogDbContext>, CatalogDataSeeder>();

        services.AddScoped<GetProductsHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddValidatorsFromAssemblyContaining<GetProductsQueryValidator>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalog");

        group.MapProductEndpoints();
    }
}
