using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularCommerce.Shared.Infrastructure.Messaging;

public static class EventBusExtensions
{
    /// <summary>
    /// DIP: MassTransit/RabbitMQ ayrıntısı Shared'da gizlenir; modüller yalnız
    /// IPublishEndpoint/IConsumer soyutlamalarını görür. Bus TEK kayıt olmalı —
    /// Program.cs'te BİR kez çağrılır (modül-başına AddMassTransit çakışır). Consumer'lar
    /// composition root'tan configureConsumers ile enjekte edilir (Shared, somut
    /// consumer tipini bilmez — bağımlılık yönü doğru kalır).
    /// </summary>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var connectionString = configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq bulunamadı");

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(connectionString));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
