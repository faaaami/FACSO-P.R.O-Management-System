using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Agent.Queries.GetAgentEmployeeById;
using PROManagementSystem.Application.Agent.Queries.GetAgentEmployeeDocuments;
using PROManagementSystem.Application.Agent.Queries.GetAgentEmployees;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent/employees")]
[Authorize]
public sealed class AgentEmployeesController : ControllerBase
{
    private readonly ISender _sender;

    public AgentEmployeesController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/agent/employees
    // ================================================================

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgentEmployeesQuery(
            page, pageSize);

        var result =
            await _sender.Send(query, cancellationToken);

        return Ok(
            ApiResponse<GetEmployeesResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/employees/{id}
    // ================================================================

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeeByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployeeById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentEmployeeByIdQuery(id);

        var result =
            await _sender.Send(query, cancellationToken);

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

    // ================================================================
    // GET /api/v1/agent/employees/{id}/documents
    // ================================================================

    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeeDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentEmployeeDocumentsQuery(id);

        var result =
            await _sender.Send(query, cancellationToken);

        return Ok(
            ApiResponse<GetEmployeeDocumentsResponseDto>
                .SuccessResponse(result));
    }
}
