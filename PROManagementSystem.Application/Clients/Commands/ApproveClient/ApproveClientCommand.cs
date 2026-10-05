using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.ApproveClient;

public sealed record ApproveClientCommand(
    Guid ClientCompanyId,
    Guid ContactId
) : IRequest<ApproveClientResponseDto>;