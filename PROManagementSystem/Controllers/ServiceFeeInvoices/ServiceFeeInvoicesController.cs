using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;
using PROManagementSystem.Application.ServiceFeeInvoices.Commands.MarkServiceFeeInvoicePaid;
using PROManagementSystem.Application.ServiceFeeInvoices.Commands.VoidServiceFeeInvoice;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoicePdf;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.ServiceFeeInvoices;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public sealed class ServiceFeeInvoicesController
    : ControllerBase
{
    private readonly ISender _sender;

    public ServiceFeeInvoicesController(
        ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("service-fee-invoices")]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceFeeInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new CreateServiceFeeInvoiceCommand(
                request.ClientCompanyId,
                request.RenewalTaskId,
                request.InvoiceNumber,
                request.InvoiceDate,
                request.DueDate,
                request.Amount,
                request.Currency,
                request.Description);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateServiceFeeInvoiceResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("service-fee-invoices")]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? clientCompanyId,
        [FromQuery] Guid? renewalTaskId,
        [FromQuery] ServiceFeeInvoiceStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query =
            new GetServiceFeeInvoicesQuery(
                clientCompanyId,
                renewalTaskId,
                status,
                page,
                pageSize);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetServiceFeeInvoicesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("service-fee-invoices/{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query =
            new GetServiceFeeInvoiceByIdQuery(id);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetServiceFeeInvoiceByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("service-fee-invoices/{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command =
            new MarkServiceFeeInvoicePaidCommand(id);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<MarkServiceFeeInvoicePaidResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("service-fee-invoices/{id:guid}/void")]
    public async Task<IActionResult> Void(
        Guid id,
        [FromBody] VoidServiceFeeInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new VoidServiceFeeInvoiceCommand(
                id,
                request.Reason);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<VoidServiceFeeInvoiceResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("service-fee-invoices/{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query =
            new GetServiceFeeInvoicePdfQuery(id);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return File(
            result.PdfBytes,
            "application/pdf",
            result.FileName);
    }
}
