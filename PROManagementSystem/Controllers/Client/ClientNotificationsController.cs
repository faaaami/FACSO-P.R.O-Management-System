using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Notifications.Commands.MarkAllNotificationsAsRead;
using PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;
using PROManagementSystem.Application.Notifications.Queries.GetNotificationUnreadCount;
using PROManagementSystem.Application.Notifications.Queries.GetNotifications;

namespace PROManagementSystem.Api.Controllers.Client;

[ApiController]
[Route("api/v1/client/notifications")]
[Authorize(Roles = "Client")]
public sealed class ClientNotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public ClientNotificationsController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/client/notifications
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
    // GET /api/v1/client/notifications/unread-count
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
    // PATCH /api/v1/client/notifications/{id}/read
    // ================================================================

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationAsReadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new MarkNotificationAsReadCommand(id);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<MarkNotificationAsReadResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/client/notifications/read-all
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