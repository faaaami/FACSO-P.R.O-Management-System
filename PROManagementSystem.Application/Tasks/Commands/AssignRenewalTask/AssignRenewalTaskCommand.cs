using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.AssignRenewalTask;

public sealed record AssignRenewalTaskCommand(
    Guid TaskId,
    Guid StaffId
) : IRequest<AssignRenewalTaskResponseDto>;