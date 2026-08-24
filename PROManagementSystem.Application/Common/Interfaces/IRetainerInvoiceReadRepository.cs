using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;
using PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IRetainerInvoiceReadRepository
{
    Task<GetRetainerInvoicesResponseDto> GetPagedAsync(
        Guid? clientCompanyId,
        Guid? serviceContractId,
        RetainerInvoiceStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetRetainerInvoiceByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
