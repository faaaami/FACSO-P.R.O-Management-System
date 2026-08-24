using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoiceById;
using PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IServiceFeeInvoiceReadRepository
{
    Task<GetServiceFeeInvoicesResponseDto> GetPagedAsync(
        Guid? clientCompanyId,
        Guid? renewalTaskId,
        ServiceFeeInvoiceStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetServiceFeeInvoiceByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
