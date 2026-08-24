namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed record CreateServiceFeeInvoiceRequest(
    Guid ClientCompanyId,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Amount,
    string Currency,
    string? Description
);
