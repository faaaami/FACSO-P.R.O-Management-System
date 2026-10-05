using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IGovFeeDisbursementReadRepository
{
    Task<GetGovFeeDisbursementsResponseDto?> GetListAsync(
        Guid? clientCompanyId,
        GovFeeDisbursementStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetGovFeeDisbursementByIdResponseDto?> GetByIdAsync(
        Guid id,
        Guid? clientCompanyId,
        CancellationToken cancellationToken);
}
