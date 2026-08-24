using PROManagementSystem.Application.Common.Realtime;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IRealtimeNotificationService
{
    Task SendTaskAssignedAsync(
        TaskAssignedEvent notification,
        CancellationToken cancellationToken);
}