using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

public sealed record CreateClientContactCommand(
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone
) : IRequest<CreateClientContactResponseDto>;