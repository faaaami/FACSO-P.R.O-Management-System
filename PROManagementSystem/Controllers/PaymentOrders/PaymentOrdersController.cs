using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.PaymentOrders.Commands.CancelPaymentOrder;
using PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;
using PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.PaymentOrders;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public sealed class PaymentOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public PaymentOrdersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("payment-orders")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePaymentOrderCommand(
            request.InvoiceType,
            request.RetainerInvoiceId,
            request.ServiceFeeInvoiceId,
            request.Notes);

        var result = await _sender.Send(
            command, cancellationToken);

        return StatusCode(
            201,
            ApiResponse<CreatePaymentOrderResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("payment-orders")]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? clientCompanyId,
        [FromQuery] PaymentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPaymentOrdersQuery(
            clientCompanyId,
            status,
            page,
            pageSize);

        var result = await _sender.Send(
            query, cancellationToken);

        return Ok(
            ApiResponse<GetPaymentOrdersResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("payment-orders/{id:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentOrderByIdQuery(id);

        var result = await _sender.Send(
            query, cancellationToken);

        return Ok(
            ApiResponse<GetPaymentOrderByIdResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("payment-orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command =
            new CancelPaymentOrderCommand(id);

        var result = await _sender.Send(
            command, cancellationToken);

        return Ok(
            ApiResponse<CancelPaymentOrderResponseDto>
                .SuccessResponse(result));
    }
}
