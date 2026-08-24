using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.CancelRenewalTask;

public sealed record CancelRenewalTaskCommand(
    Guid TaskId
) : IRequest<CancelRenewalTaskResponseDto>;