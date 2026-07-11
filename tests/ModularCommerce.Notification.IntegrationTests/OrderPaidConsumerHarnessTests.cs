using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Notification.Api.Consumers;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Notification.IntegrationTests.Fixtures;
using ModularCommerce.Notification.Infrastructure;
using ModularCommerce.Notification.Infrastructure.Channels;
using ModularCommerce.Notification.Infrastructure.Persistence;
using ModularCommerce.Ordering.Contracts.IntegrationEvents;
using Xunit;

namespace ModularCommerce.Notification.IntegrationTests;

/// <summary>
/// Gerçek OrderPaidNotificationConsumer + Definition'ı MassTransit in-memory Test Harness'ta
/// koşturur: mutlu yolda audit satırları yazılır; knob=1.0'da teslim hatası consumer'ı fault'a
/// düşürür (retry tükenince RabbitMQ'da _error kuyruğuna gider — gerçek kuyruk kanıtı manuel UI).
/// </summary>
[Collection("NotificationPostgres")]
public sealed class OrderPaidConsumerHarnessTests(NotificationPostgresFixture fixture)
{
    private ServiceProvider BuildProvider(double failureRate)
    {
        var services = new ServiceCollection();

        services.AddSingleton(new NotificationOptions { FailureRate = failureRate, LatencyMs = 0 });

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(
                fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.Schema)));

        services.AddScoped<INotificationChannel>(sp => new FaultInjectingChannel(
            new EmailNotificationChannel(sp.GetRequiredService<NotificationOptions>(), NullLogger<EmailNotificationChannel>.Instance),
            sp.GetRequiredService<NotificationOptions>()));
        services.AddScoped<INotificationChannel>(sp => new FaultInjectingChannel(
            new WebhookNotificationChannel(sp.GetRequiredService<NotificationOptions>(), NullLogger<WebhookNotificationChannel>.Instance),
            sp.GetRequiredService<NotificationOptions>()));

        services.AddScoped<INotificationProcessor, NotificationProcessor>();

        services.AddMassTransitTestHarness(x =>
            x.AddConsumer<OrderPaidNotificationConsumer, OrderPaidNotificationConsumerDefinition>());

        return services.BuildServiceProvider(true);
    }

    [Fact(DisplayName = "Mutlu yol: OrderPaid tüketilir, kanal başına bir audit satırı yazılır")]
    public async Task Consume_OrderPaid_WritesAuditLogs()
    {
        var orderId = Guid.NewGuid();
        await using var provider = BuildProvider(failureRate: 0);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new OrderPaid(orderId, Guid.NewGuid(), 250m, "TRY", DateTime.UtcNow));

        var consumerHarness = harness.GetConsumerHarness<OrderPaidNotificationConsumer>();
        (await consumerHarness.Consumed.Any<OrderPaid>()).Should().BeTrue("consumer OrderPaid'i tüketmeli");

        await using var verify = fixture.CreateContext();
        (await verify.NotificationLogs.CountAsync(n => n.OrderId == orderId)).Should().Be(2);
        (await verify.ProcessedMessages.CountAsync(p => p.IdempotencyKey == $"OrderPaid:{orderId}"))
            .Should().Be(1);
    }

    [Fact(DisplayName = "Teslim hatası (knob=1.0): consumer fault olur (retry → _error / DLQ yolu)")]
    public async Task Consume_WhenDeliveryFails_ConsumerFaults()
    {
        var orderId = Guid.NewGuid();
        await using var provider = BuildProvider(failureRate: 1.0);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new OrderPaid(orderId, Guid.NewGuid(), 99m, "TRY", DateTime.UtcNow));

        // İlk deneme knob'la fırlar → exception'lı consume kaydı oluşur (retry'lar tükenince
        // MassTransit mesajı _error kuyruğuna taşır — gerçek kuyruk yalnız RabbitMQ'da görülür).
        var consumerHarness = harness.GetConsumerHarness<OrderPaidNotificationConsumer>();
        (await consumerHarness.Consumed.Any<OrderPaid>(x => x.Exception is not null))
            .Should().BeTrue("teslim hatası consumer'ı fault'a düşürmeli");

        // Fault yolunda hiçbir audit/processed satırı kalıcı olmamalı (transaction commit olmadı).
        await using var verify = fixture.CreateContext();
        (await verify.NotificationLogs.CountAsync(n => n.OrderId == orderId)).Should().Be(0);
        (await verify.ProcessedMessages.CountAsync(p => p.IdempotencyKey == $"OrderPaid:{orderId}"))
            .Should().Be(0);
    }
}
