using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;

public sealed record CreatePaymentOrderCommand(
    PaymentInvoiceType InvoiceType,
    Guid? RetainerInvoiceId,
    Guid? ServiceFeeInvoiceId,
    string? Notes
) : IRequest<CreatePaymentOrderResponseDto>;
