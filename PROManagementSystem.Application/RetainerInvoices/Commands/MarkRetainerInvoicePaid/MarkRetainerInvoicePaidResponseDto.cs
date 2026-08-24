using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.MarkRetainerInvoicePaid;

public sealed record MarkRetainerInvoicePaidResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid ServiceContractId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    decimal Amount,
    string Currency,
    RetainerInvoiceStatus Status,
    string? Notes,
    DateTime? PaidAt,
    DateTime? VoidedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
