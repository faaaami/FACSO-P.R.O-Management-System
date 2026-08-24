using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentServiceFeeInvoice;

public sealed record GetAgentServiceFeeInvoiceResponseDto(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Amount,
    string Currency,
    ServiceFeeInvoiceStatus Status,
    string? Description,
    DateTime? PaidAt,
    DateTime CreatedAt
);
