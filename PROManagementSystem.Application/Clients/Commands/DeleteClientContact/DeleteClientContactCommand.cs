using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.DeleteClientContact;

public sealed record DeleteClientContactCommand(
    Guid ClientCompanyId,
    Guid ContactId
) : IRequest<DeleteClientContactResponseDto>;