namespace PROManagementSystem.Application.RetainerInvoices.Commands.CreateRetainerInvoice;

public sealed record CreateRetainerInvoiceRequest(
    Guid ClientCompanyId,
    Guid ServiceContractId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    decimal Amount,
    string Currency,
    string? Notes
);
