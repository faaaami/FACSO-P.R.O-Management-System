using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Client.Commands.CreateServiceRequest;
using PROManagementSystem.Application.Client.Queries.GetMyActiveContract;
using PROManagementSystem.Application.Client.Queries.GetMyCompany;
using PROManagementSystem.Application.Client.Queries.GetMyContacts;
using PROManagementSystem.Application.Client.Queries.GetMyDocumentById;
using PROManagementSystem.Application.Client.Queries.GetMyDocumentFile;
using PROManagementSystem.Application.Client.Queries.GetMyDocuments;
using PROManagementSystem.Application.Client.Queries.GetMyDocumentScan;
using PROManagementSystem.Application.Client.Queries.GetMyEmployeeById;
using PROManagementSystem.Application.Client.Queries.GetMyEmployeeDocuments;
using PROManagementSystem.Application.Client.Queries.GetMyEmployees;
using PROManagementSystem.Application.Client.Queries.GetMyEntities;
using PROManagementSystem.Application.Client.Queries.GetMyEntityById;
using PROManagementSystem.Application.Client.Queries.GetMyExpiringDocuments;
using PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskById;
using PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskHistory;
using PROManagementSystem.Application.Client.Queries.GetMyRenewalTasks;
using PROManagementSystem.Application.Client.Queries.GetMyServiceRequest;
using PROManagementSystem.Application.Client.Queries.GetMyServiceRequests;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;
using PROManagementSystem.Application.Clients.Queries.GetClientServiceReport;
using PROManagementSystem.Application.Clients.Queries.GetClientServiceReportPdf;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Contracts.Queries.GetActiveContract;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoicePdf;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoicePdf;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.Client;

[ApiController]
[Route("api/v1/client")]
[Authorize(Roles = "Client")]
public sealed class ClientController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public ClientController(
        ISender sender,
        ICurrentUserService currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Resolves the authenticated client's tenant. The tenant is ALWAYS
    /// derived from the token's client_company_id claim and never from any
    /// client-supplied value. Returns a 404 when the context carries no
    /// company association (matching the established client-surface invalid
    /// context behavior).
    /// </summary>
    private IActionResult TenantNotFound()
        => NotFound(
            ApiResponse<object>.FailureResponse(
                "CLIENT_COMPANY_NOT_FOUND",
                "Client company was not found."));

    private bool TryGetClientCompanyId(out Guid clientCompanyId)
    {
        var id = _currentUser.ClientCompanyId;

        if (id.HasValue)
        {
            clientCompanyId = id.Value;
            return true;
        }

        clientCompanyId = Guid.Empty;
        return false;
    }

    [HttpGet("company")]
    [ProducesResponseType(typeof(ApiResponse<GetClientCompanyByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCompany(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyCompanyQuery(),
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

    [HttpGet("entities")]
    [ProducesResponseType(typeof(ApiResponse<GetClientEntitiesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEntities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMyEntitiesQuery(page, pageSize, search),
            cancellationToken);

        return Ok(
            ApiResponse<GetClientEntitiesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("entities/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetClientEntityByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEntityById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyEntityByIdQuery(id),
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

    [HttpGet("contacts")]
    [ProducesResponseType(typeof(ApiResponse<GetClientContactsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetContacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMyContactsQuery(page, pageSize, search),
            cancellationToken);

        return Ok(
            ApiResponse<GetClientContactsResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("contracts/active")]
    [ProducesResponseType(typeof(ApiResponse<GetActiveContractResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveContract(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyActiveContractQuery(),
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "ACTIVE_CONTRACT_NOT_FOUND",
                    "Active contract was not found."));
        }

        return Ok(
            ApiResponse<GetActiveContractResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/employees
    // ================================================================

    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyEmployeesQuery(
            page, pageSize, entityId);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetEmployeesResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/employees/{id}
    // ================================================================

    [HttpGet("employees/{id:guid}")]
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
        var query = new GetMyEmployeeByIdQuery(id);

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

    // ================================================================
    // GET /api/v1/client/employees/{id}/documents
    // ================================================================

    [HttpGet("employees/{id:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<GetEmployeeDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployeeDocuments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetMyEmployeeDocumentsQuery(id);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetEmployeeDocumentsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/documents
    // ================================================================

    [HttpGet("documents")]
    [ProducesResponseType(typeof(ApiResponse<GetClientDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DocumentType? type = null,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] DateTime? expiresFrom = null,
        [FromQuery] DateTime? expiresTo = null,
        [FromQuery] DocumentOwnerType? ownerType = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyDocumentsQuery(
            page, pageSize, type, status, expiresFrom, expiresTo, ownerType);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetClientDocumentsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/documents/{id}
    // ================================================================

    [HttpGet("documents/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetMyDocumentByIdQuery(id);

        var result = await _sender.Send(
            query,
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "DOCUMENT_NOT_FOUND",
                        "Document was not found."));
        }

        return Ok(
            ApiResponse<GetDocumentByIdResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/documents/{id}/file
    // ================================================================

    [HttpGet("documents/{id:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentFile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetMyDocumentFileQuery(id);

        var result = await _sender.Send(
            query,
            cancellationToken);

        // Another company's document, a missing/soft-deleted document, no
        // attached file, an unsupported content type or an
        // unreadable/traversal-rejected storage reference are all
        // indistinguishable (404). The storage reference is never returned.
        if (result is null)
        {
            return NotFound(
                ApiResponse<object>
                    .FailureResponse(
                        "DOCUMENT_FILE_NOT_FOUND",
                        "Document file was not found."));
        }

        return File(
            result.Content,
            result.ContentType,
            result.FileName);
    }

    // ================================================================
    // GET /api/v1/client/documents/{id}/scan
    // ================================================================

    [HttpGet("documents/{id:guid}/scan")]
    [ProducesResponseType(typeof(ApiResponse<GetDocumentScanResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocumentScan(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetMyDocumentScanQuery(id);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetDocumentScanResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/documents/expiring
    // ================================================================

    [HttpGet("documents/expiring")]
    [ProducesResponseType(typeof(ApiResponse<GetExpiringDocumentsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetExpiringDocuments(
        [FromQuery] int days = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyExpiringDocumentsQuery(
            days, page, pageSize, includeExpired);

        var result = await _sender.Send(
            query,
            cancellationToken);

        return Ok(
            ApiResponse<GetExpiringDocumentsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/tasks
    // ================================================================

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTasksResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyRenewalTasks(
        [FromQuery] RenewalTaskStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMyRenewalTasksQuery(status, page, pageSize),
            cancellationToken);

        return Ok(
            ApiResponse<GetRenewalTasksResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/tasks/{id}
    // ================================================================

    [HttpGet("tasks/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTaskByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRenewalTaskById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyRenewalTaskByIdQuery(id),
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "RENEWAL_TASK_NOT_FOUND",
                    "Renewal task was not found."));
        }

        return Ok(
            ApiResponse<GetRenewalTaskByIdResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/tasks/{id}/history
    // ================================================================

    [HttpGet("tasks/{id:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<GetRenewalTaskHistoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRenewalTaskHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyRenewalTaskHistoryQuery(id),
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "RENEWAL_TASK_NOT_FOUND",
                    "Renewal task was not found."));
        }

        return Ok(
            ApiResponse<GetRenewalTaskHistoryResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // POST /api/v1/client/service-requests
    // ================================================================

    [HttpPost("service-requests")]
    [ProducesResponseType(typeof(ApiResponse<CreateServiceRequestResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateServiceRequest(
        [FromBody] CreateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateServiceRequestCommand(
            request.Type,
            request.EmployeeId,
            request.EntityId,
            request.Notes);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetServiceRequestById),
            new { id = result.Id },
            ApiResponse<CreateServiceRequestResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/service-requests
    // ================================================================

    [HttpGet("service-requests")]
    [ProducesResponseType(typeof(ApiResponse<GetServiceRequestsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetServiceRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ServiceRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetMyServiceRequestsQuery(page, pageSize, status),
            cancellationToken);

        return Ok(
            ApiResponse<GetServiceRequestsResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // GET /api/v1/client/service-requests/{id}
    // ================================================================

    [HttpGet("service-requests/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetServiceRequestResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetServiceRequestById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMyServiceRequestQuery(id),
            cancellationToken);

        if (result is null)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "SERVICE_REQUEST_NOT_FOUND",
                    "Service request was not found."));
        }

        return Ok(
            ApiResponse<GetServiceRequestResponseDto>
                .SuccessResponse(result));
    }

    // ================================================================
    // Week 3 Day 4 - Stage 2: client billing / payment / service report
    // ================================================================

    // GET /api/v1/client/retainer-invoices
    [HttpGet("retainer-invoices")]
    [ProducesResponseType(typeof(ApiResponse<GetRetainerInvoicesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyRetainerInvoices(
        [FromQuery] RetainerInvoiceStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetRetainerInvoicesQuery(
                clientCompanyId,
                null,
                status,
                page,
                pageSize),
            cancellationToken);

        return Ok(
            ApiResponse<GetRetainerInvoicesResponseDto>
                .SuccessResponse(result));
    }

    // GET /api/v1/client/retainer-invoices/{id}
    [HttpGet("retainer-invoices/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetRetainerInvoiceByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyRetainerInvoiceById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetRetainerInvoiceByIdQuery(
                id, clientCompanyId),
            cancellationToken);

        return Ok(
            ApiResponse<GetRetainerInvoiceByIdResponseDto>
                .SuccessResponse(result));
    }

    // GET /api/v1/client/retainer-invoices/{id}/pdf
    [HttpGet("retainer-invoices/{id:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyRetainerInvoicePdf(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetRetainerInvoicePdfQuery(
                id, clientCompanyId),
            cancellationToken);

        return File(
            result.PdfBytes,
            "application/pdf",
            result.FileName);
    }

    // GET /api/v1/client/service-fee-invoices
    [HttpGet("service-fee-invoices")]
    [ProducesResponseType(typeof(ApiResponse<GetServiceFeeInvoicesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyServiceFeeInvoices(
        [FromQuery] ServiceFeeInvoiceStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetServiceFeeInvoicesQuery(
                clientCompanyId,
                null,
                status,
                page,
                pageSize),
            cancellationToken);

        return Ok(
            ApiResponse<GetServiceFeeInvoicesResponseDto>
                .SuccessResponse(result));
    }

    // GET /api/v1/client/service-fee-invoices/{id}
    [HttpGet("service-fee-invoices/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetServiceFeeInvoiceByIdResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyServiceFeeInvoiceById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetServiceFeeInvoiceByIdQuery(
                id, clientCompanyId),
            cancellationToken);

        return Ok(
            ApiResponse<GetServiceFeeInvoiceByIdResponseDto>
                .SuccessResponse(result));
    }

    // GET /api/v1/client/service-fee-invoices/{id}/pdf
    [HttpGet("service-fee-invoices/{id:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyServiceFeeInvoicePdf(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetServiceFeeInvoicePdfQuery(
                id, clientCompanyId),
            cancellationToken);

        return File(
            result.PdfBytes,
            "application/pdf",
            result.FileName);
    }

    // GET /api/v1/client/gov-fee-disbursements
    [HttpGet("gov-fee-disbursements")]
    [ProducesResponseType(typeof(ApiResponse<GetGovFeeDisbursementsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyGovFeeDisbursements(
        [FromQuery] GovFeeDisbursementStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetGovFeeDisbursementsQuery(
                clientCompanyId,
                status,
                page,
                pageSize),
            cancellationToken);

        return Ok(
            ApiResponse<GetGovFeeDisbursementsResponseDto>
                .SuccessResponse(result));
    }

    // POST /api/v1/client/payments/create-order
    [HttpPost("payments/create-order")]
    [ProducesResponseType(typeof(ApiResponse<CreatePaymentOrderResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateMyPaymentOrder(
        [FromBody] CreateClientPaymentOrderRequest request,
        CancellationToken cancellationToken)
    {
        Guid? retainerInvoiceId = null;
        Guid? serviceFeeInvoiceId = null;

        if (request.InvoiceType is null)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "INVALID_INVOICE_TYPE",
                    "invoiceType must be either 'Retainer' or 'ServiceFee'."));
        }

        switch (request.InvoiceType.Value)
        {
            case ClientPaymentInvoiceType.Retainer:
                retainerInvoiceId = request.InvoiceId;
                break;

            case ClientPaymentInvoiceType.ServiceFee:
                serviceFeeInvoiceId = request.InvoiceId;
                break;

            default:
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "INVALID_INVOICE_TYPE",
                        "invoiceType must be either 'Retainer' or 'ServiceFee'."));
        }

        var command = new CreatePaymentOrderCommand(
            retainerInvoiceId.HasValue
                ? PaymentInvoiceType.RetainerInvoice
                : PaymentInvoiceType.ServiceFeeInvoice,
            retainerInvoiceId,
            serviceFeeInvoiceId,
            null);

        var result = await _sender.Send(
            command,
            cancellationToken);

        return Ok(
            ApiResponse<CreatePaymentOrderResponseDto>
                .SuccessResponse(result));
    }

    // GET /api/v1/client/payments
    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<GetPaymentOrdersResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyPayments(
        [FromQuery] PaymentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var result = await _sender.Send(
            new GetPaymentOrdersQuery(
                clientCompanyId,
                status,
                page,
                pageSize),
            cancellationToken);

        return Ok(
            ApiResponse<GetPaymentOrdersResponseDto>
                .SuccessResponse(result));
    }

    // GET /api/v1/client/service-report
    [HttpGet("service-report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyServiceReportPdf(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!TryGetClientCompanyId(out var clientCompanyId))
        {
            return TenantNotFound();
        }

        var pdfBytes = await _sender.Send(
            new GetClientServiceReportPdfQuery(
                clientCompanyId,
                from,
                to),
            cancellationToken);

        return File(
            pdfBytes,
            "application/pdf",
            $"service-report-{clientCompanyId:N}.pdf");
    }
}