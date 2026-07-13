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
using ModularCommerce.Shared.Infrastructure.Configuration;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Notification.Api;

public sealed class NotificationModule : IModule
{
    public string Name => "Notification";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationDbContext>(configuration, NotificationDbContext.Schema);
        services.AddValidatedOptions<NotificationOptions>(configuration, NotificationOptions.SectionName);

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

        group.MapNotificationDevEndpoints(isDevelopment);
    }
}
