using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;

public sealed record CreatePaymentOrderRequest(
    PaymentInvoiceType InvoiceType,
    Guid? RetainerInvoiceId,
    Guid? ServiceFeeInvoiceId,
    string? Notes
);
