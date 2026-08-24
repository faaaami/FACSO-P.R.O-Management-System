using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.DeleteClientEntity;

public sealed record DeleteClientEntityCommand(
    Guid ClientCompanyId,
    Guid EntityId
) : IRequest<DeleteClientEntityResponseDto>;