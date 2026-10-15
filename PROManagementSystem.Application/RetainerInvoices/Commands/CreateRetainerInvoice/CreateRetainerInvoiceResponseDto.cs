using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.CreateRetainerInvoice;

public sealed record CreateRetainerInvoiceResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid ServiceContractId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    decimal Amount,
    string Currency,
    RetainerInvoiceStatus Status,
    string? Notes,
    DateTime CreatedAt
);
