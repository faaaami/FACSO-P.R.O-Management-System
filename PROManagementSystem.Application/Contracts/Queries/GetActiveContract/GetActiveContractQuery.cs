using MediatR;

namespace PROManagementSystem.Application.Contracts.Queries.GetActiveContract;

public sealed record GetActiveContractQuery(
    Guid ClientCompanyId
) : IRequest<GetActiveContractResponseDto?>;