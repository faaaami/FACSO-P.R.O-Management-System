using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Api.Controllers.Tasks;
using PROManagementSystem.Application.Tasks.Commands.AddRenewalStep;
using PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;
using PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;
using PROManagementSystem.Application.Tasks.Queries.GetAgentTaskHistory;
using PROManagementSystem.Application.Tasks.Queries.GetAgentTaskById;
using PROManagementSystem.Application.Tasks.Queries.GetAgentServiceFeeInvoice;
using PROManagementSystem.Application.Tasks.Queries.GetAgentTasks;
using PROManagementSystem.Application.Tasks.Queries.GetAgentTaskSteps;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent/tasks")]
[Authorize]
public sealed class AgentTasksController : ControllerBase
{
    private readonly ISender _sender;

    public AgentTasksController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/agent/tasks
    // ================================================================

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTasksResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTasks(
        [FromQuery] RenewalTaskStatus? status,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentTasksQuery(status);

        var result =
            await _sender.Send(query, cancellationToken);

        return Ok(
            ApiResponse<GetRenewalTasksResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/tasks/{id}
    // ================================================================

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTaskByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentTaskByIdQuery(id);

        var result =
            await _sender.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "NOT_FOUND",
                        "Renewal task was not found.",
                        null));
        }

        return Ok(
            ApiResponse<GetRenewalTaskByIdResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/tasks/{taskId}/status
    // ================================================================

    [HttpPatch("{taskId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<UpdateRenewalTaskStatusResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateStatus(
        Guid taskId,
        [FromBody] UpdateRenewalTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new UpdateRenewalTaskStatusCommand(
                taskId,
                request.Status,
                request.Note);

        var result =
            await _sender.Send(command, cancellationToken);

        return Ok(
            ApiResponse<UpdateRenewalTaskStatusResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // PATCH /api/v1/agent/tasks/{taskId}/block
    // ================================================================

    [HttpPatch("{taskId:guid}/block")]
    [ProducesResponseType(typeof(ApiResponse<BlockRenewalTaskResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Block(
        Guid taskId,
        [FromBody] BlockRenewalTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new BlockRenewalTaskCommand(
                taskId,
                request.BlockedReason);

        var result =
            await _sender.Send(command, cancellationToken);

        return Ok(
            ApiResponse<BlockRenewalTaskResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/tasks/{taskId}/history
    // ================================================================

    [HttpGet("{taskId:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTaskHistoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistory(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentTaskHistoryQuery(taskId);

        var result =
            await _sender.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "NOT_FOUND",
                        "Renewal task was not found.",
                        null));
        }

        return Ok(
            ApiResponse<GetRenewalTaskHistoryResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/tasks/{taskId}/steps
    // ================================================================

    [HttpGet("{taskId:guid}/steps")]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTaskStepsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSteps(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentTaskStepsQuery(taskId);

        var result =
            await _sender.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "NOT_FOUND",
                        "Renewal task was not found.",
                        null));
        }

        return Ok(
            ApiResponse<GetRenewalTaskStepsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // POST /api/v1/agent/tasks/{taskId}/steps
    // ================================================================

    [HttpPost("{taskId:guid}/steps")]
    [ProducesResponseType(typeof(ApiResponse<AddRenewalStepResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddStep(
        Guid taskId,
        [FromBody] AddRenewalStepRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new AddRenewalStepCommand(
                taskId,
                request.StepName,
                request.ReferenceNumber,
                request.ProofFileUrl);

        var result =
            await _sender.Send(command, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AddRenewalStepResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/tasks/{taskId}/service-fee-invoice
    // ================================================================

    [HttpGet("{taskId:guid}/service-fee-invoice")]
    [ProducesResponseType(typeof(ApiResponse<GetAgentServiceFeeInvoiceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetServiceFeeInvoice(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentServiceFeeInvoiceQuery(taskId);

        var result =
            await _sender.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "NOT_FOUND",
                        "Service fee invoice was not found.",
                        null));
        }

        return Ok(
            ApiResponse<GetAgentServiceFeeInvoiceResponseDto>
                .SuccessResponse(result));
    }
}
