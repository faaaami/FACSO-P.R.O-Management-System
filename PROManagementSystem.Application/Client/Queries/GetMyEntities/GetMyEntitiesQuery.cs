using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;

namespace PROManagementSystem.Application.Client.Queries.GetMyEntities;

public sealed record GetMyEntitiesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<GetClientEntitiesResponseDto>;