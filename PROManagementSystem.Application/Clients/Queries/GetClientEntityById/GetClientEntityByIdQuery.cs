using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityById;

public sealed record GetClientEntityByIdQuery(
    Guid ClientCompanyId,
    Guid EntityId,
    bool IncludeDeleted = false
) : IRequest<GetClientEntityByIdResponseDto?>;