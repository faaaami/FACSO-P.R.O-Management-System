using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Notifications.Commands.MarkAllNotificationsAsRead;
using PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;
using PROManagementSystem.Application.Notifications.Queries.GetNotificationUnreadCount;
using PROManagementSystem.Application.Notifications.Queries.GetNotifications;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent/notifications")]
[Authorize(Roles = "Agent")]
public sealed class AgentNotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public AgentNotificationsController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/agent/notifications
    // ================================================================

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetNotificationsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetNotificationsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/notifications/unread-count
    // ================================================================

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetNotificationUnreadCountQuery(),
            cancellationToken);

        return Ok(
            ApiResponse<int>.SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/notifications/{notificationId}/read
    // ================================================================

    [HttpPatch("{notificationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationAsReadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new MarkNotificationAsReadCommand(notificationId),
            cancellationToken);

        return Ok(
            ApiResponse<MarkNotificationAsReadResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/notifications/read-all
    // ================================================================

    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(ApiResponse<MarkAllNotificationsAsReadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
