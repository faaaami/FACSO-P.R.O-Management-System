using MediatR;

namespace PROManagementSystem.Application.Contracts.Queries.GetServiceContract;

public sealed record GetServiceContractQuery(
    Guid Id
) : IRequest<GetServiceContractResponseDto>;