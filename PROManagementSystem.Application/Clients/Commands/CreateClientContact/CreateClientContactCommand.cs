using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

/// <summary>
/// Invites a client contact. There is no password: the contact receives an
/// invitation email and sets their own.
/// </summary>
public sealed record CreateClientContactCommand(
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone
) : IRequest<CreateClientContactResponseDto>;
