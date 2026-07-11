namespace ModularCommerce.Notification.Application.Delivery;
public sealed record NotificationOptions
{
    public const string SectionName = "Notification:Delivery";
    public double FailureRate { get; init; }
    public int LatencyMs { get; init; } = 20;
}
