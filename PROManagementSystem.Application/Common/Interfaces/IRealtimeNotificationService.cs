using PROManagementSystem.Application.Common.Realtime;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IRealtimeNotificationService
{
    Task SendTaskAssignedAsync(
        TaskAssignedEvent notification,
        CancellationToken cancellationToken);

    Task SendTaskStatusChangedAsync(
        TaskStatusChangedEvent notification,
        CancellationToken cancellationToken);

    Task SendNotificationReceivedAsync(
        Guid userId,
        NotificationReceivedDto notification,
        CancellationToken cancellationToken);
}