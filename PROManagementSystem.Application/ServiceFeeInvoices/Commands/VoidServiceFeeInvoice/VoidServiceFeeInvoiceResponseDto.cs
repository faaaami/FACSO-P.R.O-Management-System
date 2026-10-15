using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.VoidServiceFeeInvoice;

public sealed record VoidServiceFeeInvoiceResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal Amount,
    string Currency,
    ServiceFeeInvoiceStatus Status,
    string? Description,
    DateTime? PaidAt,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
