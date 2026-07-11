using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Notification.Application.Abstractions;
public interface INotificationProcessor
{
    Task<Result> ProcessAsync(NotificationInstruction instruction, CancellationToken cancellationToken);
}
