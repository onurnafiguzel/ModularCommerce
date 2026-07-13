using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Cart.Api.Endpoints;
using ModularCommerce.Cart.Application.Carts.AddItem;
using ModularCommerce.Cart.Application.Carts.GetCart;
using ModularCommerce.Cart.Application.Carts.RemoveItem;
using ModularCommerce.Cart.Application.Carts.UpdateItemQuantity;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Cart.Infrastructure.Persistence;
using ModularCommerce.Shared.Infrastructure.Modules;

namespace ModularCommerce.Cart.Api;

public sealed class CartModule : IModule
{
    public string Name => "Cart";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICartRepository, RedisCartRepository>();

        services.AddScoped<ModularCommerce.Cart.Contracts.ICartService, Application.Carts.Contracts.CartService>();
        services.AddScoped<GetCartHandler>();
        services.AddScoped<AddItemHandler>();
        services.AddScoped<UpdateItemQuantityHandler>();
        services.AddScoped<RemoveItemHandler>();
        services.AddValidatorsFromAssemblyContaining<AddItemCommandValidator>(includeInternalTypes: true);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cart");

        group.MapCartEndpoints();
    }
}
