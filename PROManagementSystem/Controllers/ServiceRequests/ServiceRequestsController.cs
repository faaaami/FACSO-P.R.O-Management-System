using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Commands.RejectServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

namespace PROManagementSystem.Api.Controllers.ServiceRequests;

[ApiController]
[Route("api/v1/service-requests")]
[Authorize]
public sealed class ServiceRequestsController : ControllerBase
{
    private readonly ISender _sender;

    public ServiceRequestsController(
        ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetServiceRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query =
            new GetServiceRequestsQuery(
                page,
                pageSize);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetServiceRequestsResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetServiceRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query =
            new GetServiceRequestQuery(id);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetServiceRequestResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{id:guid}/convert")]
    public async Task<IActionResult> Convert(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command =
            new ConvertServiceRequestCommand(id);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<ConvertServiceRequestResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
    Guid id,
    [FromBody] RejectServiceRequestRequest request,
    CancellationToken cancellationToken)
    {
        var command =
            new RejectServiceRequestCommand(
                id,
                request.Reason);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<RejectServiceRequestResponseDto>
                .SuccessResponse(result));
    }
}