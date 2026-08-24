namespace PROManagementSystem.Application.Common.Interfaces;

public interface IInvoicePdfService
{
    Task<byte[]> GenerateAsync(
        InvoicePdfModel invoice,
        CancellationToken cancellationToken = default);
}

public sealed record InvoicePdfModel(
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    string ClientCompanyName,
    string? ContractNumber,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    decimal Amount,
    string Currency,
    string Status,
    string? Notes,
    Guid? RenewalTaskId = null,
    string? InvoiceType = "Retainer"
);
