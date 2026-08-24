using MediatR;

namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;

public sealed record GetGovFeeDisbursementByIdQuery(
    Guid Id
) : IRequest<GetGovFeeDisbursementByIdResponseDto>;
