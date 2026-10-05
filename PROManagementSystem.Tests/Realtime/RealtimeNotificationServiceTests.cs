using Microsoft.AspNetCore.SignalR;
using PROManagementSystem.Api.Common.Services;
using PROManagementSystem.Api.Hubs;
using PROManagementSystem.Application.Common.Realtime;
using Xunit;

namespace PROManagementSystem.Tests.Realtime;

/// <summary>
/// Unit-level verification that the SignalR NotificationHub backplane pushes
/// are targeted at the correct per-user group ("user:{{userId}}") and use the
/// expected method names. Uses a hand-rolled IHubContext for a real
/// RealtimeNotificationService (the API test host substitutes a NoOp).
/// </summary>
public sealed class RealtimeNotificationServiceTests
{
    [Fact(DisplayName =
        "SendNotificationReceivedAsync pushes to the user group with the NotificationReceived method")]
    public async Task SendNotificationReceivedAsync_TargetsUserGroup()
    {
        var hub = new FakeHubContext();
        var service = new RealtimeNotificationService(hub);
        var userId = Guid.NewGuid();

        var payload = new NotificationReceivedDto(
            Guid.NewGuid(),
            "Test title",
            "Test message",
            false,
            DateTime.UtcNow);

        await service.SendNotificationReceivedAsync(
            userId,
            payload,
            CancellationToken.None);

        var group = Assert.Single(
            ((FakeHubClients)hub.Clients).GroupTargets);
        Assert.Equal($"user:{userId}", group.GroupName);

        var sent = Assert.Single(group.Proxy.Sent);
        Assert.Equal("NotificationReceived", sent.Method);

        var received = Assert.IsType<NotificationReceivedDto>(sent.Args[0]);
        Assert.Equal(payload.Id, received.Id);
        Assert.Equal(payload.Title, received.Title);
        Assert.Equal(payload.Message, received.Message);
    }

    [Fact(DisplayName =
        "SendTaskAssignedAsync pushes to the staff user group with the TaskAssigned method")]
    public async Task SendTaskAssignedAsync_TargetsStaffGroup()
    {
        var hub = new FakeHubContext();
        var service = new RealtimeNotificationService(hub);
        var staffId = Guid.NewGuid();

        var notification = new TaskAssignedEvent(
            Guid.NewGuid(),
            staffId,
            "Agent A",
            Guid.NewGuid(),
            "DOC-A-001");

        await service.SendTaskAssignedAsync(
            notification,
            CancellationToken.None);

        var group = Assert.Single(
            ((FakeHubClients)hub.Clients).GroupTargets);
        Assert.Equal($"user:{staffId}", group.GroupName);

        var sent = Assert.Single(group.Proxy.Sent);
        Assert.Equal("TaskAssigned", sent.Method);
    }
}

internal sealed class FakeHubContext : IHubContext<NotificationHub>
{
    public IHubClients Clients { get; } =
        new FakeHubClients();

    public IGroupManager Groups { get; } =
        new FakeGroupManager();
}

internal sealed class FakeHubClients : IHubClients
{
    public List<(string GroupName, FakeClientProxy Proxy)> GroupTargets
        { get; } = new();

    public IClientProxy All
        => throw new NotSupportedException();

    public IClientProxy AllExcept(
        IReadOnlyList<string> excludedConnectionIds)
        => throw new NotSupportedException();

    public IClientProxy Client(string connectionId)
        => throw new NotSupportedException();

    public IClientProxy Group(string groupName)
    {
        var proxy = new FakeClientProxy();
        GroupTargets.Add((groupName, proxy));
        return proxy;
    }

    public IClientProxy Groups(
        IReadOnlyList<string> groupNames)
        => throw new NotSupportedException();

    public IClientProxy GroupExcept(
        string groupName,
        IReadOnlyList<string> excludedConnectionIds)
        => throw new NotSupportedException();

    public IClientProxy User(string userId)
        => throw new NotSupportedException();

    public IClientProxy Users(
        IReadOnlyList<string> userIds)
        => throw new NotSupportedException();

    public IClientProxy Clients(
        IReadOnlyList<string> connectionIds)
        => throw new NotSupportedException();
}

internal sealed class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> Sent { get; } = new();

    public Task SendCoreAsync(
        string method,
        object?[] args,
        CancellationToken cancellationToken = default)
    {
        Sent.Add((method, args));
        return Task.CompletedTask;
    }
}

internal sealed class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveFromGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}