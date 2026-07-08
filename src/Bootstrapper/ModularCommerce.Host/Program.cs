using ModularCommerce.Notification.Api.Consumers;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.ExceptionHandling;
using ModularCommerce.Shared.Infrastructure.Messaging;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Observability;
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

builder.Services.AddEventBus(builder.Configuration, consumers =>
    consumers.AddConsumer<OrderPaidLoggingConsumer>());

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

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
