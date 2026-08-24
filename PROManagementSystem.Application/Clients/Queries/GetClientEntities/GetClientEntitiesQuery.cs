using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntities;

public sealed record GetClientEntitiesQuery(
    Guid ClientCompanyId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeDeleted = false
) : IRequest<GetClientEntitiesResponseDto>;