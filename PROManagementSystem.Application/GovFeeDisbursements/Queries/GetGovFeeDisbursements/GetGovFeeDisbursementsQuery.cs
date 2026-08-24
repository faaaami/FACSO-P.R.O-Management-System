using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;

public sealed record GetGovFeeDisbursementsQuery(
    Guid? ClientCompanyId,
    GovFeeDisbursementStatus? Status,
    int Page,
    int PageSize
) : IRequest<GetGovFeeDisbursementsResponseDto>;
