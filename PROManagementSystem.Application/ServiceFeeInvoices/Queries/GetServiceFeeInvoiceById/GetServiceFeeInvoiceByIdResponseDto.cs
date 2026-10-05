using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;

public sealed record GetServiceFeeInvoiceByIdResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ClientCompanyName,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Amount,
    string Currency,
    ServiceFeeInvoiceStatus Status,
    string? Description,
    DateTime? PaidAt,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? PaymentOrderId,
    PaymentStatus? PaymentOrderStatus,
    string? RazorpayOrderId
);
