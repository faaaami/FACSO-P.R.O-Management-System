using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;

namespace PROManagementSystem.Application.Client.Queries.GetMyEntityById;

public sealed record GetMyEntityByIdQuery(
    Guid EntityId
) : IRequest<GetClientEntityByIdResponseDto?>;