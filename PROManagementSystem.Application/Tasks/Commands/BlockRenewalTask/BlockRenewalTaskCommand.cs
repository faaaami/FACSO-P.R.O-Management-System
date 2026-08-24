using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;

public sealed record BlockRenewalTaskCommand(
    Guid TaskId,
    string BlockedReason
) : IRequest<BlockRenewalTaskResponseDto>;