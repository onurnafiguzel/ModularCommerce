using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Notification.IntegrationTests.Fixtures;
using ModularCommerce.Notification.Infrastructure;
using ModularCommerce.Notification.Infrastructure.Persistence;
using ModularCommerce.Shared.Kernel;
using Xunit;

namespace ModularCommerce.Notification.IntegrationTests;

/// <summary>
/// El yapımı idempotent inbox'ın çekirdek kanıtları (NFR-8.1) gerçek Postgres'e karşı:
/// tek gönderim seti, iş-anahtarı bazlı dedup ve eşzamanlı çift teslimde PK hakemi.
/// </summary>
[Collection("NotificationPostgres")]
public sealed class ProcessorIdempotencyTests(NotificationPostgresFixture fixture)
{
    private const string ConsumerType = "OrderPaidNotificationConsumer";

    /// <summary>Çağrı sayan sahte kanal — processor'ın gönderimi kaç kez yaptığını ölçer.</summary>
    private sealed class CountingChannel(string name) : INotificationChannel
    {
        private int _calls;
        public int Calls => _calls;
        public string Name => name;

        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.CompletedTask;
        }
    }

    private static NotificationInstruction Instruction(Guid orderId, Guid? messageId = null)
        => new(
            IdempotencyKey: $"OrderPaid:{orderId}",
            ConsumerType: ConsumerType,
            OrderId: orderId,
            MessageId: messageId ?? Guid.NewGuid(),
            Message: new NotificationMessage(
                $"customer-{orderId}@example.com", "Siparişiniz onaylandı", "Ödemeniz alındı."));

    private NotificationProcessor CreateProcessor(NotificationDbContext context, params INotificationChannel[] channels)
        => new(context, channels, NullLogger<NotificationProcessor>.Instance);

    [Fact(DisplayName = "İlk işleme: kanal başına bir audit satırı + bir processed satırı yazar")]
    public async Task FirstTime_WritesOneLogPerChannel_AndOneProcessedRow()
    {
        var orderId = Guid.NewGuid();
        var email = new CountingChannel("email");
        var webhook = new CountingChannel("webhook");

        await using (var context = fixture.CreateContext())
        {
            var result = await CreateProcessor(context, email, webhook)
                .ProcessAsync(Instruction(orderId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        email.Calls.Should().Be(1);
        webhook.Calls.Should().Be(1);

        await using var verify = fixture.CreateContext();
        (await verify.NotificationLogs.CountAsync(n => n.OrderId == orderId)).Should().Be(2);
        (await verify.ProcessedMessages.CountAsync(p => p.IdempotencyKey == $"OrderPaid:{orderId}"))
            .Should().Be(1);
    }

    [Fact(DisplayName = "Aynı OrderId (farklı MessageId) ikinci kez: idempotent — tekrar göndermez")]
    public async Task SameOrder_DifferentMessageId_IsIdempotent()
    {
        var orderId = Guid.NewGuid();
        var email = new CountingChannel("email");
        var webhook = new CountingChannel("webhook");

        // 1. teslim (bir MessageId).
        await using (var context1 = fixture.CreateContext())
        {
            await CreateProcessor(context1, email, webhook)
                .ProcessAsync(Instruction(orderId, Guid.NewGuid()), CancellationToken.None);
        }

        // 2. teslim: outbox republish → FARKLI MessageId, AYNI iş anahtarı. MessageId ile
        // dedup edilseydi tekrar gönderilirdi; iş anahtarı (OrderId) doğru şekilde durdurur.
        await using (var context2 = fixture.CreateContext())
        {
            var result = await CreateProcessor(context2, email, webhook)
                .ProcessAsync(Instruction(orderId, Guid.NewGuid()), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        email.Calls.Should().Be(1, "ikinci teslim hızlı-yolda atlanmalı");
        webhook.Calls.Should().Be(1);

        await using var verify = fixture.CreateContext();
        (await verify.NotificationLogs.CountAsync(n => n.OrderId == orderId)).Should().Be(2);
        (await verify.ProcessedMessages.CountAsync(p => p.IdempotencyKey == $"OrderPaid:{orderId}"))
            .Should().Be(1);
    }

    [Fact(DisplayName = "Eşzamanlı çift teslim: PK hakemi birini eler — tek satır seti, iki başarı")]
    public async Task ConcurrentDoubleDelivery_PkArbiterKeepsOneSet()
    {
        var orderId = Guid.NewGuid();

        // Her iki teslim de AYRI context/kanal ile eşzamanlı koşar: ikisi de AnyAsync kontrolünü
        // boş geçse bile processed_messages bileşik PK'sı yalnız birinin insert'ine izin verir;
        // kaybeden 23505 alıp idempotent başarı döner.
        async Task<Result> Deliver()
        {
            await using var context = fixture.CreateContext();
            return await CreateProcessor(context, new CountingChannel("email"), new CountingChannel("webhook"))
                .ProcessAsync(Instruction(orderId), CancellationToken.None);
        }

        var results = await Task.WhenAll(Deliver(), Deliver());

        results.Should().OnlyContain(r => r.IsSuccess, "her iki teslim de idempotent başarı dönmeli");

        await using var verify = fixture.CreateContext();
        (await verify.ProcessedMessages.CountAsync(p => p.IdempotencyKey == $"OrderPaid:{orderId}"))
            .Should().Be(1, "PK hakemi yalnız bir processed satırına izin verir");
        (await verify.NotificationLogs.CountAsync(n => n.OrderId == orderId))
            .Should().Be(2, "yalnız kazananın audit satırları kalıcı olur (kaybedenin transaction'ı geri sarılır)");
    }
}
