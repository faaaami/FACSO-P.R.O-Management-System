using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;
using PROManagementSystem.Application.Tasks.Queries.GetAgentWorkload;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent")]
[Authorize(Roles = "Agent")]
public sealed class AgentController : ControllerBase
{
    private readonly ISender _sender;

    public AgentController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/agent/workload
    // ================================================================

    [HttpGet("workload")]
    [ProducesResponseType(typeof(ApiResponse<GetStaffWorkloadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWorkload(
        CancellationToken cancellationToken)
    {
        var query = new GetAgentWorkloadQuery();

        var result =
            await _sender.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "NOT_FOUND",
                        "Agent workload was not found.",
                        null));
        }

        return Ok(
            ApiResponse<GetStaffWorkloadResponseDto>
                .SuccessResponse(result));
    }
}
