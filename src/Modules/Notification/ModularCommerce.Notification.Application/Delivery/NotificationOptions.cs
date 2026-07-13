using System.ComponentModel.DataAnnotations;
namespace ModularCommerce.Notification.Application.Delivery;
public sealed record NotificationOptions
{
    public const string SectionName = "Notification:Delivery";
    [Range(0.0, 1.0)]
    public double FailureRate { get; init; }
    [Range(0, int.MaxValue)]
    public int LatencyMs { get; init; } = 20;
}
