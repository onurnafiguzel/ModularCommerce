using ModularCommerce.Shared.Infrastructure.Modules;

var builder = WebApplication.CreateBuilder(args);

IModule[] modules =
[
    new ModularCommerce.Identity.Api.IdentityModule(),
    new ModularCommerce.Catalog.Api.CatalogModule(),
    new ModularCommerce.Cart.Api.CartModule(),
    new ModularCommerce.Inventory.Api.InventoryModule(),
    new ModularCommerce.Ordering.Api.OrderingModule(),
    new ModularCommerce.Payment.Api.PaymentModule(),
    new ModularCommerce.Shipping.Api.ShippingModule(),
    new ModularCommerce.Notification.Api.NotificationModule(),
];

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    application = "ModularCommerce",
    modules = modules.Select(m => m.Name),
}));

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();
