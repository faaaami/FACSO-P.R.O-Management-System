using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientContact;

public sealed record UpdateClientContactCommand(
    Guid ClientCompanyId,
    Guid ContactId,
    UserRole Role,
    bool IsActive
) : IRequest<UpdateClientContactResponseDto>;