using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Test-only replacement for the SignalR/Redis realtime backend. Prevents
/// API tests from attempting to connect to an unreachable Redis/SignalR
/// when status/assignment changes publish notifications.
/// </summary>
public sealed class NoOpRealtimeNotificationService
    : IRealtimeNotificationService
{
    public Task SendTaskAssignedAsync(
        TaskAssignedEvent notification,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SendTaskStatusChangedAsync(
        TaskStatusChangedEvent notification,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
