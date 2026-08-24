using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace PROManagementSystem.Api.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId =
            Context.User?
                .FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userId, out var id))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetUserGroup(id));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        var userId =
            Context.User?
                .FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userId, out var id))
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                GetUserGroup(id));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string GetUserGroup(Guid userId)
        => $"user:{userId}";
}