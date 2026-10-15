using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IServiceFeeInvoiceCreationService
{
    Task<ServiceFeeInvoice> CreateAsync(
        Guid clientCompanyId,
        Guid renewalTaskId,
        decimal amount,
        string currency,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string invoiceNumber,
        string? description,
        CancellationToken cancellationToken);
}
