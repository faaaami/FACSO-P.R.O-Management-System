using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClientEntities;

public sealed record GetAgentClientEntitiesQuery(
    Guid ClientId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<GetClientEntitiesResponseDto>;
