using FluentAssertions;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Notification.Infrastructure.Channels;
using Xunit;

namespace ModularCommerce.Notification.UnitTests;

/// <summary>
/// DLQ demo'sunun kalbi olan knob decorator'ının davranışı: FailureRate=1.0 her gönderimi
/// hataya çevirir (retry tükenir → _error kuyruğu), 0 ise iç kanala şeffaf delege eder
/// (normal koşu deterministik).
/// </summary>
public sealed class FaultInjectingChannelTests
{
    private sealed class RecordingChannel : INotificationChannel
    {
        public int Calls { get; private set; }
        public string Name => "email";

        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private static readonly NotificationMessage Message =
        new("customer@example.com", "Onay", "Ödeme alındı");

    [Fact(DisplayName = "FailureRate=1.0 → teslim hatası fırlatır, iç kanal çağrılmaz")]
    public async Task KnobOn_Throws_AndDoesNotCallInner()
    {
        var inner = new RecordingChannel();
        var channel = new FaultInjectingChannel(inner, new NotificationOptions { FailureRate = 1.0 });

        var act = () => channel.SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<NotificationDeliveryException>();
        inner.Calls.Should().Be(0);
    }

    [Fact(DisplayName = "FailureRate=0 → iç kanala şeffaf delege eder")]
    public async Task KnobOff_Delegates_ToInner()
    {
        var inner = new RecordingChannel();
        var channel = new FaultInjectingChannel(inner, new NotificationOptions { FailureRate = 0 });

        await channel.SendAsync(Message, CancellationToken.None);

        inner.Calls.Should().Be(1);
        channel.Name.Should().Be("email", "decorator iç kanalın adını korur");
    }
}
