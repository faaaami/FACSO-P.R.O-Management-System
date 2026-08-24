using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CancelPaymentOrder;

public sealed record CancelPaymentOrderResponseDto(
    Guid Id,
    PaymentInvoiceType InvoiceType,
    Guid? RetainerInvoiceId,
    Guid? ServiceFeeInvoiceId,
    Guid ClientCompanyId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTime? CancelledAt,
    DateTime UpdatedAt
);
