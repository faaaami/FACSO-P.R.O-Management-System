using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed record CreateServiceFeeInvoiceResponseDto(
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
    DateTime CreatedAt
);
