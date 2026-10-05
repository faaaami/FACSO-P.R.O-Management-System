using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.RejectClient;

public sealed record RejectClientCommand(
    Guid ClientCompanyId,
    Guid ContactId
) : IRequest<RejectClientResponseDto>;