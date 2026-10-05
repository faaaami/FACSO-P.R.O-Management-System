using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Employees.Commands.CreateEmployee;
using PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;
using PROManagementSystem.Application.Employees.Commands.DeleteEmployee;
using PROManagementSystem.Application.Employees.Commands.UpdateEmployee;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;
using PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

namespace PROManagementSystem.Api.Controllers.Employees;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class EmployeesController : ControllerBase
{
    private readonly ISender _sender;

    public EmployeesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("entities/{entityId:guid}/employees")]
    [ProducesResponseType(typeof(ApiResponse<CreateEmployeeResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        Guid entityId,
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeCommand(
            entityId,
            request.FullName,
            request.PassportNumber,
            request.DateOfBirth,
            request.Nationality,
            request.JobTitle,
            request.HireDate);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateEmployeeResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployees(
    [FromQuery] Guid? clientId,
    [FromQuery] Guid? entityId,
    [FromQuery] bool? isActive,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetEmployeesQuery(
            clientId,
            entityId,
            isActive,
            page,
            pageSize);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetEmployeesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("employees/{employeeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeeByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
    Guid employeeId,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetEmployeeByIdQuery(
            employeeId,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "EMPLOYEE_NOT_FOUND",
                        "Employee was not found."));
        }

        return Ok(
            ApiResponse<GetEmployeeByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("employees/{employeeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateEmployeeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
    Guid employeeId,
    [FromBody] UpdateEmployeeRequestDto request,
    CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeCommand(
            employeeId,
            request.FullName,
            request.PassportNumber,
            request.DateOfBirth,
            request.Nationality,
            request.JobTitle,
            request.HireDate,
            request.IsActive);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateEmployeeResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete("employees/{employeeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteEmployeeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
    Guid employeeId,
    CancellationToken cancellationToken)
    {
        var command = new DeleteEmployeeCommand(
            employeeId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeleteEmployeeResponseDto>
                .SuccessResponse(result));
    }

    [HttpPost("employees/{employeeId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<CreateEmployeeDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateDocument(
    Guid employeeId,
    [FromBody] CreateEmployeeDocumentRequestDto request,
    CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeDocumentCommand(
            employeeId,
            request.Type,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.FileSize,
            request.Details);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateEmployeeDocumentResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("employees/{employeeId:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeeDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocuments(
    Guid employeeId,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetEmployeeDocumentsQuery(
            employeeId,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetEmployeeDocumentsResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("employees/expiring")]
    [ProducesResponseType(typeof(ApiResponse<GetExpiringEmployeesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExpiringEmployees(
    [FromQuery] int days = 30,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] bool includeExpired = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetExpiringEmployeesQuery(
            days,
            page,
            pageSize,
            includeExpired);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetExpiringEmployeesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("employees/{employeeId:guid}/timeline")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeeTimelineResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTimeline(
    Guid employeeId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetEmployeeTimelineQuery(
            employeeId,
            page,
            pageSize);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetEmployeeTimelineResponseDto>
                .SuccessResponse(result));
    }
}
