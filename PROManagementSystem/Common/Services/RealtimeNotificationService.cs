using Microsoft.AspNetCore.SignalR;
using PROManagementSystem.Api.Hubs;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;

namespace PROManagementSystem.Api.Common.Services;

public sealed class RealtimeNotificationService
    : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public RealtimeNotificationService(
        IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendTaskAssignedAsync(
        TaskAssignedEvent notification,
        CancellationToken cancellationToken)
    {
        var groupName =
            $"user:{notification.StaffId}";

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync(
                "TaskAssigned",
                notification,
                cancellationToken);
    }

    public async Task SendTaskStatusChangedAsync(
        TaskStatusChangedEvent notification,
        CancellationToken cancellationToken)
    {
        var groupName =
            $"user:{notification.StaffId}";

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync(
                "TaskStatusChanged",
                notification,
                cancellationToken);
    }
}