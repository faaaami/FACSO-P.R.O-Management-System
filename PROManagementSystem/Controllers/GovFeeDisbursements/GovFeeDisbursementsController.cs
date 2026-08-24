using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.GovFeeDisbursements.Commands.CreateGovFeeDisbursement;
using PROManagementSystem.Application.GovFeeDisbursements.Commands.UpdateGovFeeDisbursementStatus;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.GovFeeDisbursements;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class GovFeeDisbursementsController : ControllerBase
{
    private readonly ISender _sender;

    public GovFeeDisbursementsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("gov-fee-disbursements")]
    public async Task<IActionResult> Create(
        [FromBody] CreateGovFeeDisbursementRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateGovFeeDisbursementCommand(
            request.ClientCompanyId,
            request.RenewalTaskId,
            request.FeeDescription,
            request.GovernmentReference,
            request.Amount,
            request.Currency,
            request.Notes);

        var result = await _sender.Send(
            command, cancellationToken);

        return StatusCode(
            201,
            ApiResponse<CreateGovFeeDisbursementResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("gov-fee-disbursements")]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? clientCompanyId,
        [FromQuery] GovFeeDisbursementStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetGovFeeDisbursementsQuery(
            clientCompanyId,
            status,
            page,
            pageSize);

        var result = await _sender.Send(
            query, cancellationToken);

        return Ok(
            ApiResponse<GetGovFeeDisbursementsResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("gov-fee-disbursements/{id:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetGovFeeDisbursementByIdQuery(id);

        var result = await _sender.Send(
            query, cancellationToken);

        return Ok(
            ApiResponse<GetGovFeeDisbursementByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("gov-fee-disbursements/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateGovFeeDisbursementStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new UpdateGovFeeDisbursementStatusCommand(
                id,
                request.Status);

        var result = await _sender.Send(
            command, cancellationToken);

        return Ok(
            ApiResponse<UpdateGovFeeDisbursementStatusResponseDto>
                .SuccessResponse(result));
    }
}
