namespace PROManagementSystem.Application.RetainerInvoices.Commands.CreateRetainerInvoice;

public sealed record CreateRetainerInvoiceRequest(
    Guid ClientCompanyId,
    Guid ServiceContractId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    decimal Amount,
    string Currency,
    string? Notes
);
