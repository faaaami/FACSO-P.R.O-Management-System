using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;

public sealed record CreatePaymentOrderResponseDto(
    Guid Id,
    PaymentInvoiceType InvoiceType,
    Guid? RetainerInvoiceId,
    Guid? ServiceFeeInvoiceId,
    Guid ClientCompanyId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? RazorpayOrderId,
    int RazorpayAmount,
    string RazorpayKeyId,
    DateTime CreatedAt
);
