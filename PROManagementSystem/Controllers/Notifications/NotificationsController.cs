using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Notifications.Queries.GetNotifications;
using PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;
using PROManagementSystem.Application.Notifications.Commands.MarkAllNotificationsAsRead;
using PROManagementSystem.Application.Notifications.Queries.GetNotificationUnreadCount;

namespace PROManagementSystem.Api.Controllers.Notifications;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetNotificationsResponseDto>.SuccessResponse(
                result));
    }

    // Unread count for the signed-in user, whatever their role.
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetNotificationUnreadCountQuery(),
            cancellationToken);

        return Ok(
            ApiResponse<int>.SuccessResponse(result));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new MarkNotificationAsReadCommand(id);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<MarkNotificationAsReadResponseDto>.SuccessResponse(
                result));
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new MarkAllNotificationsAsReadCommand(),
            cancellationToken);

        return Ok(
            ApiResponse<MarkAllNotificationsAsReadResponseDto>
                .SuccessResponse(result));
    }
}