using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Staff.Commands.CreateStaff;
using PROManagementSystem.Application.Staff.Commands.DeactivateStaff;
using PROManagementSystem.Application.Staff.Commands.UpdateStaff;
using PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;
using PROManagementSystem.Application.Staff.Queries.GetStaff;
using PROManagementSystem.Application.Staff.Queries.GetStaffById;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

namespace PROManagementSystem.Api.Controllers.Staff;

[ApiController]
[Route("api/v1/admin/staff")]
[Authorize(Roles = "Admin")]
public sealed class StaffController : ControllerBase
{
    private readonly ISender _sender;

    public StaffController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateStaffResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateStaffCommand(
            request.FullName,
            request.Email,
            request.Password,
            request.Phone);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateStaffResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStaff(
    [FromQuery] bool includeInactive = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetStaffQuery(
            includeInactive);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetStaffResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{staffId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetStaffByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
    Guid staffId,
    CancellationToken cancellationToken)
    {
        var query = new GetStaffByIdQuery(
            staffId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "STAFF_NOT_FOUND",
                    "Staff member was not found."));
        }

        return Ok(
            ApiResponse<GetStaffByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{staffId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
    Guid staffId,
    [FromBody] UpdateStaffRequestDto request,
    CancellationToken cancellationToken)
    {
        var command = new UpdateStaffCommand(
            staffId,
            request.FullName,
            request.Email,
            request.Phone,
            request.IsApproved);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateStaffResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete("{staffId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeactivateStaffResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Deactivate(
    Guid staffId,
    CancellationToken cancellationToken)
    {
        var command = new DeactivateStaffCommand(
            staffId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeactivateStaffResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{staffId:guid}/workload")]
    [ProducesResponseType(typeof(ApiResponse<GetStaffWorkloadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWorkload(
    Guid staffId,
    CancellationToken cancellationToken)
    {
        var query =
            new GetStaffWorkloadQuery(staffId);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "STAFF_NOT_FOUND",
                    "Staff member was not found."));
        }

        return Ok(
            ApiResponse<GetStaffWorkloadResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("workload")]
    [ProducesResponseType(typeof(ApiResponse<GetAllStaffWorkloadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllWorkload(
    CancellationToken cancellationToken)
    {
        var query =
            new GetAllStaffWorkloadQuery();

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetAllStaffWorkloadResponseDto>
                .SuccessResponse(result));
    }
}
