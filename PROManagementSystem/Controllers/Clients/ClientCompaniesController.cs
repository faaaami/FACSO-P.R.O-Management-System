using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Clients.Commands.ApproveClient;
using PROManagementSystem.Application.Clients.Commands.CreateClientCompany;
using PROManagementSystem.Application.Clients.Commands.CreateClientContact;
using PROManagementSystem.Application.Clients.Commands.CreateClientEntity;
using PROManagementSystem.Application.Clients.Commands.DeleteClientCompany;
using PROManagementSystem.Application.Clients.Commands.DeleteClientContact;
using PROManagementSystem.Application.Clients.Commands.DeleteClientEntity;
using PROManagementSystem.Application.Clients.Commands.RejectClient;
using PROManagementSystem.Application.Clients.Commands.UpdateClientCompany;
using PROManagementSystem.Application.Clients.Commands.UpdateClientContact;
using PROManagementSystem.Application.Clients.Commands.UpdateClientEntity;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;
using PROManagementSystem.Application.Clients.Queries.GetClientServiceReport;
using PROManagementSystem.Application.Clients.Queries.GetClientServiceReportPdf;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Api.Controllers.Clients;

[ApiController]
[Route("api/v1/admin/clients")]
[Authorize(Roles = "Admin")]
public sealed class ClientCompaniesController : ControllerBase
{
    private readonly ISender _sender;

    public ClientCompaniesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateClientCompanyResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateClientCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateClientCompanyResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetClientCompaniesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll(
    [FromQuery] GetClientCompaniesQuery query,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetClientCompaniesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetClientCompanyByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
    Guid id,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetClientCompanyByIdQuery(
            id,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "CLIENT_COMPANY_NOT_FOUND",
                    "Client company was not found."));
        }

        return Ok(
            ApiResponse<GetClientCompanyByIdResponseDto>
                .SuccessResponse(result));
    }


    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateClientCompanyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
    Guid id,
    [FromBody] UpdateClientCompanyCommand command,
    CancellationToken cancellationToken)
    {
        var commandWithId = command with
        {
            Id = id
        };

        var result = await _sender.Send(
            commandWithId,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateClientCompanyResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteClientCompanyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new DeleteClientCompanyCommand(id);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeleteClientCompanyResponseDto>
                .SuccessResponse(result));
    }


    [HttpPost("{clientId:guid}/entities")]
    [ProducesResponseType(typeof(ApiResponse<CreateClientEntityResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateEntity(
        Guid clientId,
        [FromBody] CreateClientEntityCommand request,
        CancellationToken cancellationToken)
    {
        var command = request with
        {
            ClientCompanyId = clientId
        };

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateClientEntityResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{clientId:guid}/entities")]
    [ProducesResponseType(typeof(ApiResponse<GetClientEntitiesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEntities(
    Guid clientId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetClientEntitiesQuery(
            clientId,
            page,
            pageSize,
            search,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetClientEntitiesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{clientId:guid}/entities/{entityId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetClientEntityByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEntityById(
    Guid clientId,
    Guid entityId,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetClientEntityByIdQuery(
            clientId,
            entityId,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "CLIENT_ENTITY_NOT_FOUND",
                    "Client entity was not found."));
        }

        return Ok(
            ApiResponse<GetClientEntityByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{clientId:guid}/entities/{entityId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateClientEntityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEntity(
    Guid clientId,
    Guid entityId,
    [FromBody] UpdateClientEntityCommand request,
    CancellationToken cancellationToken)
    {
        var command = request with
        {
            ClientCompanyId = clientId,
            EntityId = entityId
        };

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateClientEntityResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete("{clientId:guid}/entities/{entityId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteClientEntityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteEntity(
    Guid clientId,
    Guid entityId,
    CancellationToken cancellationToken)
    {
        var command = new DeleteClientEntityCommand(
            clientId,
            entityId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeleteClientEntityResponseDto>
                .SuccessResponse(result));
    }

    [HttpPost("{clientId:guid}/contacts")]
    [ProducesResponseType(typeof(ApiResponse<CreateClientContactResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateContact(
    Guid clientId,
    [FromBody] CreateClientContactCommand request,
    CancellationToken cancellationToken)
    {
        var command = request with
        {
            ClientCompanyId = clientId
        };

        var result = await _sender.Send(
            command,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateClientContactResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{clientId:guid}/contacts")]
    [ProducesResponseType(typeof(ApiResponse<GetClientContactsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetContacts(
    Guid clientId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] bool includeDeleted = false,
    CancellationToken cancellationToken = default)
    {
        var query = new GetClientContactsQuery(
            clientId,
            page,
            pageSize,
            search,
            includeDeleted);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetClientContactsResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{clientId:guid}/contacts/{contactId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UpdateClientContactResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateContact(
    Guid clientId,
    Guid contactId,
    [FromBody] UpdateClientContactCommand request,
    CancellationToken cancellationToken)
    {
        var command = request with
        {
            ClientCompanyId = clientId,
            ContactId = contactId
        };

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<UpdateClientContactResponseDto>
                .SuccessResponse(result));
    }

    [HttpDelete("{clientId:guid}/contacts/{contactId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteClientContactResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteContact(
    Guid clientId,
    Guid contactId,
    CancellationToken cancellationToken)
    {
        var command = new DeleteClientContactCommand(
            clientId,
            contactId);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<DeleteClientContactResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{clientId:guid}/contacts/{contactId:guid}/approval")]
    [ProducesResponseType(typeof(ApiResponse<ApproveClientResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateContactApproval(
    Guid clientId,
    Guid contactId,
    [FromBody] ClientApprovalRequest request,
    CancellationToken cancellationToken)
    {
        if (request.Approved is null)
        {
            throw new ArgumentException(
                "The 'approved' value is required.");
        }

        if (request.Approved.Value)
        {
            var approve = await _sender.Send(
                new ApproveClientCommand(clientId, contactId),
                cancellationToken);

            return Ok(
                ApiResponse<ApproveClientResponseDto>
                    .SuccessResponse(approve));
        }

        var reject = await _sender.Send(
            new RejectClientCommand(clientId, contactId),
            cancellationToken);

        return Ok(
            ApiResponse<RejectClientResponseDto>
                .SuccessResponse(reject));
    }

    [HttpGet("{id:guid}/service-report")]
    [ProducesResponseType(typeof(ApiResponse<ClientServiceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetServiceReport(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = new GetClientServiceReportQuery(
            id, from, to);

        var result = await _sender.Send(
            query, cancellationToken);

        return Ok(
            ApiResponse<ClientServiceReportDto>
                .SuccessResponse(result));
    }

[HttpGet("{id:guid}/service-report/pdf")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetServiceReportPdf(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = new GetClientServiceReportPdfQuery(
            id, from, to);

        var pdfBytes = await _sender.Send(
            query, cancellationToken);

        return File(
            pdfBytes,
            "application/pdf",
            $"service-report-{id:N}.pdf");
    }
}
