using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModularCommerce.Notification.Api.Endpoints;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Notification.Infrastructure;
using ModularCommerce.Notification.Infrastructure.Channels;
using ModularCommerce.Notification.Infrastructure.Persistence;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Notification.Api;

public sealed class NotificationModule : IModule
{
    public string Name => "Notification";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationDbContext>(configuration, NotificationDbContext.Schema);

        // Knob (PSP options deseni): config'ten bind + validate; singleton olarak enjekte edilir.
        var deliveryOptions = configuration.GetSection(NotificationOptions.SectionName)
            .Get<NotificationOptions>() ?? new NotificationOptions();
        ValidateOptions(deliveryOptions);
        services.AddSingleton(deliveryOptions);

        // Kanallar (Strategy) her biri FaultInjectingChannel (Decorator) ile sarılı: hata
        // enjeksiyonu tek noktadan (knob) açılıp kapanır, kanallar temiz kalır. IEnumerable
        // olarak çözülür → processor hepsine gönderir (OCP: yeni kanal = yeni kayıt).
        services.AddScoped<INotificationChannel>(sp => new FaultInjectingChannel(
            new EmailNotificationChannel(
                sp.GetRequiredService<NotificationOptions>(),
                sp.GetRequiredService<ILogger<EmailNotificationChannel>>()),
            sp.GetRequiredService<NotificationOptions>()));

        services.AddScoped<INotificationChannel>(sp => new FaultInjectingChannel(
            new WebhookNotificationChannel(
                sp.GetRequiredService<NotificationOptions>(),
                sp.GetRequiredService<ILogger<WebhookNotificationChannel>>()),
            sp.GetRequiredService<NotificationOptions>()));

        services.AddScoped<INotificationProcessor, NotificationProcessor>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notification");
        var isDevelopment = endpoints.ServiceProvider
            .GetRequiredService<IHostEnvironment>()
            .IsDevelopment();

        group.MapGet("/health", () => Results.Ok(new { module = "Notification", status = "healthy" }));
        group.MapNotificationDevEndpoints(isDevelopment);
    }

    private static void ValidateOptions(NotificationOptions options)
    {
        if (options.FailureRate is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Notification:Delivery:FailureRate 0 ile 1 arasında olmalıdır.");
        }

        if (options.LatencyMs < 0)
        {
            throw new InvalidOperationException(
                "Notification:Delivery:LatencyMs negatif olamaz.");
        }
    }
}
