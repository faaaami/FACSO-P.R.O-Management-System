using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Tasks.Commands.AddRenewalStep;
using PROManagementSystem.Application.Tasks.Commands.AssignRenewalTask;
using PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;
using PROManagementSystem.Application.Tasks.Commands.CancelRenewalTask;
using PROManagementSystem.Application.Tasks.Commands.CreateRenewalTask;
using PROManagementSystem.Application.Tasks.Commands.SetRenewalTaskServiceFee;
using PROManagementSystem.Application.Tasks.Commands.UnblockRenewalTask;
using PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.Tasks;

[ApiController]
[Route("api/v1/admin/tasks")]
[Authorize(Roles = "Admin")]
public sealed class TasksController : ControllerBase
{
    private readonly ISender _sender;

    public TasksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateRenewalTaskResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRenewalTaskRequestDto request,
        CancellationToken cancellationToken)
    {
        var command =
            new CreateRenewalTaskCommand(
                request.DocumentId);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateRenewalTaskResponseDto>
                .SuccessResponse(result));
    }


    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTasksResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTasks(
    [FromQuery] RenewalTaskStatus? status,
    [FromQuery] Guid? assignedStaffId,
    [FromQuery] Guid? clientCompanyId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query =
            new GetRenewalTasksQuery(
                status,
                assignedStaffId,
                clientCompanyId,
                page,
                pageSize);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetRenewalTasksResponseDto>
                .SuccessResponse(result));
    }

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
        var query =
            new GetRenewalTaskByIdQuery(id);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

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

    [HttpPatch("{taskId:guid}/assign")]
    [ProducesResponseType(typeof(ApiResponse<AssignRenewalTaskResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Assign(
    Guid taskId,
    [FromBody] AssignRenewalTaskRequest request,
    CancellationToken cancellationToken)
    {
        var command =
            new AssignRenewalTaskCommand(
                taskId,
                request.StaffId);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<AssignRenewalTaskResponseDto>
                .SuccessResponse(result));
    }

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
                  request.Note,
                  request.RenewedExpiryDate);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<UpdateRenewalTaskStatusResponseDto>
                .SuccessResponse(result));
    }

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
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<BlockRenewalTaskResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{taskId:guid}/unblock")]
    [ProducesResponseType(typeof(ApiResponse<UnblockRenewalTaskResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Unblock(
        Guid taskId,
        [FromBody] UnblockRenewalTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new UnblockRenewalTaskCommand(
                taskId,
                request.UnblockReason);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<UnblockRenewalTaskResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{taskId:guid}/service-fee")]
    [ProducesResponseType(typeof(ApiResponse<SetRenewalTaskServiceFeeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetServiceFee(
        Guid taskId,
        [FromBody] SetRenewalTaskServiceFeeRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new SetRenewalTaskServiceFeeCommand(
                taskId,
                request.Amount);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<SetRenewalTaskServiceFeeResponseDto>
                .SuccessResponse(result));
    }


    [HttpDelete("{taskId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CancelRenewalTaskResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Cancel(
    Guid taskId,
    CancellationToken cancellationToken)
    {
        var command =
            new CancelRenewalTaskCommand(taskId);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<CancelRenewalTaskResponseDto>
                .SuccessResponse(result));
    }



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
            await _sender.Send(
                command,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AddRenewalStepResponseDto>
                .SuccessResponse(result));
    }

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
        var query =
            new GetRenewalTaskStepsQuery(taskId);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

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
        var query =
            new GetRenewalTaskHistoryQuery(taskId);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

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
}
