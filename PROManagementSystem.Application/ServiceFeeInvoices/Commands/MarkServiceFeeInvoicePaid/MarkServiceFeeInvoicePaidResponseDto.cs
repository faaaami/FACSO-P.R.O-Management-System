using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.MarkServiceFeeInvoicePaid;

public sealed record MarkServiceFeeInvoicePaidResponseDto(
    Guid Id,
    Guid ClientCompanyId,
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);
