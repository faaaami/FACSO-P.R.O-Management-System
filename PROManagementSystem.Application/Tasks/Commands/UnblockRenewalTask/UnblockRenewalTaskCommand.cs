using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.UnblockRenewalTask;

public sealed record UnblockRenewalTaskCommand(
    Guid TaskId,
    string UnblockReason
) : IRequest<UnblockRenewalTaskResponseDto>;
