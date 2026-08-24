using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IServiceFeeInvoiceCreationService
{
    Task<ServiceFeeInvoice> CreateAsync(
        Guid clientCompanyId,
        Guid renewalTaskId,
        decimal amount,
        string currency,
        DateTime invoiceDate,
        DateTime dueDate,
        string invoiceNumber,
        string? description,
        CancellationToken cancellationToken);
}
