using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.AuditLogs.Queries.GetAuditLog;

namespace PROManagementSystem.Api.Controllers.AuditLog;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public sealed class AuditLogController : ControllerBase
{
    private readonly ISender _sender;

    public AuditLogController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("audit-log")]
    [ProducesResponseType(typeof(ApiResponse<GetAuditLogResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditLogQuery(
            entityType,
            entityId,
            userId,
            from,
            to,
            page,
            pageSize);

        var result = await _sender.Send(
            query, cancellationToken);

        return Ok(
            ApiResponse<GetAuditLogResponseDto>
                .SuccessResponse(result));
    }
}
