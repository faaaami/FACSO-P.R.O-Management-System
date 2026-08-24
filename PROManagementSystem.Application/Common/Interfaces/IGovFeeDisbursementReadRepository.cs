using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;
using PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IGovFeeDisbursementReadRepository
{
    Task<GetGovFeeDisbursementsResponseDto?> GetListAsync(
        Guid? clientCompanyId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GetGovFeeDisbursementByIdResponseDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
