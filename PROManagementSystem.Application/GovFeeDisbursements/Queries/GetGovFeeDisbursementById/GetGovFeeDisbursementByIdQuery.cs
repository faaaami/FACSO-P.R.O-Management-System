using MediatR;

namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;

public sealed record GetGovFeeDisbursementByIdQuery(
    Guid Id,
    Guid? ClientCompanyId = null
) : IRequest<GetGovFeeDisbursementByIdResponseDto>;
