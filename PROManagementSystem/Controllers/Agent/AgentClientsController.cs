using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Agent.Queries.GetAgentClientById;
using PROManagementSystem.Application.Agent.Queries.GetAgentClientEntities;
using PROManagementSystem.Application.Agent.Queries.GetAgentClients;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;

namespace PROManagementSystem.Api.Controllers.Agent;

[ApiController]
[Route("api/v1/agent/clients")]
[Authorize(Roles = "Agent")]
public sealed class AgentClientsController : ControllerBase
{
    private readonly ISender _sender;

    public AgentClientsController(ISender sender)
    {
        _sender = sender;
    }

    // ================================================================
    // GET /api/v1/agent/clients
    // ================================================================

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetClientCompaniesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgentClientsQuery(
            page, pageSize, search);

        var result =
            await _sender.Send(query, cancellationToken);

        return Ok(
            ApiResponse<GetClientCompaniesResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/clients/{id}
    // ================================================================

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetClientCompanyByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetClientById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAgentClientByIdQuery(id);

        var result =
            await _sender.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "CLIENT_COMPANY_NOT_FOUND",
                        "Client company was not found."));
        }

        return Ok(
            ApiResponse<GetClientCompanyByIdResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/agent/clients/{id}/entities
    // ================================================================

    [HttpGet("{id:guid}/entities")]
    [ProducesResponseType(typeof(ApiResponse<GetClientEntitiesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEntities(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgentClientEntitiesQuery(
            id, page, pageSize, search);

        var result =
            await _sender.Send(query, cancellationToken);

        return Ok(
            ApiResponse<GetClientEntitiesResponseDto>
                .SuccessResponse(result));
    }
}
