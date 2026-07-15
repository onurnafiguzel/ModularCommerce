using MassTransit;
using ModularCommerce.Discovery.Api.Consumers;
using ModularCommerce.Notification.Api.Consumers;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.ExceptionHandling;
using ModularCommerce.Shared.Infrastructure.Messaging;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Observability;
using ModularCommerce.Shared.Infrastructure.RateLimiting;
using ModularCommerce.Shared.Infrastructure.Redis;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddRedis(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        if (context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }

        if (context.Exception is not null &&
            context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            context.ProblemDetails.Extensions["exception"] = context.Exception.ToString();
        }
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddDependencyHealthChecks();

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
    new ModularCommerce.Discovery.Api.DiscoveryModule(),
];

// AddEventBus, modüllerin Register'ından ÖNCE: MassTransit'in bus hosted service'i, modüllerin outbox
// dispatcher'larından (ve migrate+seed'den) ÖNCE başlar → tüketici kuyruk binding'leri hazır olur.
// Aksi halde seed anında (startup) publish edilen event'ler henüz bağlanmamış exchange'e gidip DÜŞER
// (OrderPaid bunu görmez çünkü yalnız runtime'da yayınlanır; Catalog ise seed'de yayınlar).
builder.Services.AddEventBus(builder.Configuration, consumers =>
{
    consumers.AddConsumer<OrderPaidNotificationConsumer, OrderPaidNotificationConsumerDefinition>();
    consumers.AddConsumer<ProductChangedConsumer, ProductChangedConsumerDefinition>();
});

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    application = "ModularCommerce",
    modules = modules.Select(m => m.Name),
})).DisableRateLimiting();

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();
