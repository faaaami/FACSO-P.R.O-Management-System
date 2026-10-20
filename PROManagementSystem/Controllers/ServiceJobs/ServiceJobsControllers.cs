using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Application.ServiceJobs;
using PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequestToJob;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.ServiceJobs;

public sealed record ReasonRequest(string? Reason);

public sealed record CompleteJobRequest(
    string? Note, string? RenewedDocumentNumber, DateTime? RenewedIssueDate, DateTime? RenewedExpiryDate);

public sealed record JobStepRequest(ServiceJobStepStatus Status, string? ReferenceNumber, string? Notes);

public sealed record JobDocumentRequest(bool IsReceived);

public sealed record JobNoteRequest(string Note, bool IsInternal);

public sealed record JobGovFeeRequest(
    string FeeDescription, string? GovernmentReference, decimal Amount, DateTime? PaidAt, string? Notes);

public sealed record AgentJobDetailsRequest(string? ApplicationNumber, string? Notes);

public sealed record AdminJobDetailsRequest(
    string? ApplicationNumber, string? Notes, DateOnly? DueDate, ServiceJobPriority? Priority, decimal? ServiceFee);

public sealed record AssignJobRequest(Guid? AssignedStaffId);

public sealed record ConvertToJobRequest(
    Guid? ServiceDefinitionId, Guid? AssignedStaffId, ServiceJobPriority Priority, decimal? ServiceFee,
    DateOnly? DueDate, string? Notes);

/// <summary>
/// Actions staff (admin and agent) share. Scope is enforced in the handlers:
/// an agent only reaches jobs assigned to them.
/// </summary>
public abstract class StaffServiceJobsControllerBase : ControllerBase
{
    protected readonly ISender Sender;

    protected StaffServiceJobsControllerBase(ISender sender) => Sender = sender;

    protected IActionResult JobResult(ServiceJobDto job) => Ok(ApiResponse<ServiceJobDto>.SuccessResponse(job));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new GetServiceJobByIdQuery(id), cancellationToken));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        Ok(ApiResponse<ServiceJobSummaryDto>.SuccessResponse(
            await Sender.Send(new GetServiceJobSummaryQuery(), cancellationToken)));

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new TransitionServiceJobCommand(id, ServiceJobTransition.Start, null), cancellationToken));

    [HttpPost("{id:guid}/hold")]
    public async Task<IActionResult> Hold(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new TransitionServiceJobCommand(id, ServiceJobTransition.Hold, request.Reason), cancellationToken));

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new TransitionServiceJobCommand(id, ServiceJobTransition.Resume, null), cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteJobRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new CompleteServiceJobCommand(id, request.Note, request.RenewedDocumentNumber,
            request.RenewedIssueDate, request.RenewedExpiryDate), cancellationToken));

    [HttpPatch("{id:guid}/steps/{stepId:guid}")]
    public async Task<IActionResult> UpdateStep(
        Guid id, Guid stepId, [FromBody] JobStepRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new UpdateServiceJobStepCommand(id, stepId, request.Status,
            request.ReferenceNumber, request.Notes), cancellationToken));

    [HttpPatch("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> SetDocument(
        Guid id, Guid documentId, [FromBody] JobDocumentRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new SetServiceJobDocumentReceivedCommand(id, documentId, request.IsReceived), cancellationToken));

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] JobNoteRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new AddServiceJobNoteCommand(id, request.Note, request.IsInternal), cancellationToken));

    [HttpPost("{id:guid}/government-fees")]
    public async Task<IActionResult> AddGovFee(Guid id, [FromBody] JobGovFeeRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new RecordServiceJobGovFeeCommand(id, request.FeeDescription,
            request.GovernmentReference, request.Amount, request.PaidAt, request.Notes), cancellationToken));
}

[ApiController]
[Route("api/v1/admin/service-jobs")]
[Authorize(Roles = "Admin")]
public sealed class AdminServiceJobsController : StaffServiceJobsControllerBase
{
    public AdminServiceJobsController(ISender sender) : base(sender) { }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ServiceJobStatus? status,
        [FromQuery] bool openOnly,
        [FromQuery] Guid? serviceDefinitionId,
        [FromQuery] ServiceCategory? category,
        [FromQuery] Guid? clientCompanyId,
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? assignedStaffId,
        [FromQuery] bool unassignedOnly,
        [FromQuery] bool overdueOnly,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetServiceJobsQuery(status, openOnly, serviceDefinitionId, category,
            clientCompanyId, employeeId, assignedStaffId, unassignedOnly, overdueOnly, search, page, pageSize),
            cancellationToken);
        return Ok(ApiResponse<PaginatedList<ServiceJobListItemDto>>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceJobCommand request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ServiceJobDto>.SuccessResponse(result));
    }

    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignJobRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new AssignServiceJobCommand(id, request.AssignedStaffId), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new TransitionServiceJobCommand(id, ServiceJobTransition.Cancel, request.Reason), cancellationToken));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateDetails(
        Guid id, [FromBody] AdminJobDetailsRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new UpdateServiceJobDetailsCommand(id, request.ApplicationNumber, request.Notes,
            request.DueDate, request.Priority, request.ServiceFee), cancellationToken));

    /// <summary>Converts a client service request into a job for any catalogue service.</summary>
    [HttpPost("from-request/{serviceRequestId:guid}")]
    public async Task<IActionResult> ConvertRequest(
        Guid serviceRequestId, [FromBody] ConvertToJobRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ConvertServiceRequestToJobCommand(serviceRequestId, request.ServiceDefinitionId,
            request.AssignedStaffId, request.Priority, request.ServiceFee, request.DueDate, request.Notes), cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ConvertServiceRequestToJobResponseDto>.SuccessResponse(result));
    }
}

[ApiController]
[Route("api/v1/agent/service-jobs")]
[Authorize(Roles = "Agent")]
public sealed class AgentServiceJobsController : StaffServiceJobsControllerBase
{
    public AgentServiceJobsController(ISender sender) : base(sender) { }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ServiceJobStatus? status,
        [FromQuery] bool openOnly,
        [FromQuery] ServiceCategory? category,
        [FromQuery] bool overdueOnly,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Scope (assigned to me) is applied by the handler.
        var result = await Sender.Send(new GetServiceJobsQuery(status, openOnly, null, category, null, null, null,
            false, overdueOnly, search, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PaginatedList<ServiceJobListItemDto>>.SuccessResponse(result));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateDetails(
        Guid id, [FromBody] AgentJobDetailsRequest request, CancellationToken cancellationToken) =>
        JobResult(await Sender.Send(new UpdateServiceJobDetailsCommand(id, request.ApplicationNumber, request.Notes,
            null, null, null), cancellationToken));
}

/// <summary>Client portal: read-only view of the company's own jobs.</summary>
[ApiController]
[Route("api/v1/client/service-jobs")]
[Authorize(Roles = "Client")]
public sealed class ClientServiceJobsController : ControllerBase
{
    private readonly ISender _sender;

    public ClientServiceJobsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ServiceJobStatus? status,
        [FromQuery] bool openOnly,
        [FromQuery] Guid? employeeId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetServiceJobsQuery(status, openOnly, null, null, null, employeeId, null,
            false, false, search, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PaginatedList<ServiceJobListItemDto>>.SuccessResponse(result));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        Ok(ApiResponse<ServiceJobSummaryDto>.SuccessResponse(
            await _sender.Send(new GetServiceJobSummaryQuery(), cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ServiceJobDto>.SuccessResponse(await _sender.Send(new GetServiceJobByIdQuery(id), cancellationToken)));
}
