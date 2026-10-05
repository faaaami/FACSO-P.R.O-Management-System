using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.RetainerInvoices.Commands.CreateRetainerInvoice;
using PROManagementSystem.Application.RetainerInvoices.Commands.MarkRetainerInvoicePaid;
using PROManagementSystem.Application.RetainerInvoices.Commands.VoidRetainerInvoice;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoicePdf;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.RetainerInvoices;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public sealed class RetainerInvoicesController
    : ControllerBase
{
    private readonly ISender _sender;

    public RetainerInvoicesController(
        ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("retainer-invoices")]
    public async Task<IActionResult> Create(
        [FromBody] CreateRetainerInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new CreateRetainerInvoiceCommand(
                request.ClientCompanyId,
                request.ServiceContractId,
                request.InvoiceNumber,
                request.InvoiceDate,
                request.DueDate,
                request.PeriodStart,
                request.PeriodEnd,
                request.Amount,
                request.Currency,
                request.Notes);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateRetainerInvoiceResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("retainer-invoices")]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? clientCompanyId,
        [FromQuery] Guid? serviceContractId,
        [FromQuery] RetainerInvoiceStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query =
            new GetRetainerInvoicesQuery(
                clientCompanyId,
                serviceContractId,
                status,
                page,
                pageSize);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetRetainerInvoicesResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("retainer-invoices/{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query =
            new GetRetainerInvoiceByIdQuery(id);

        var result =
            await _sender.Send(
                query,
                cancellationToken);

        return Ok(
            ApiResponse<GetRetainerInvoiceByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("retainer-invoices/{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command =
            new MarkRetainerInvoicePaidCommand(id);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<MarkRetainerInvoicePaidResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("retainer-invoices/{id:guid}/void")]
    public async Task<IActionResult> Void(
        Guid id,
        [FromBody] VoidRetainerInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new VoidRetainerInvoiceCommand(
                id,
                request.Reason);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<VoidRetainerInvoiceResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("retainer-invoices/{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query =
            new GetRetainerInvoicePdfQuery(id);

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
