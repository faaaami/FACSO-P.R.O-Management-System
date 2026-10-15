namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed record CreateServiceFeeInvoiceRequest(
    Guid ClientCompanyId,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal Amount,
    string Currency,
    string? Description
);
