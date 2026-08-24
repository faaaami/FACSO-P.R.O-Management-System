using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.CreateRenewalTask;

public sealed record CreateRenewalTaskCommand(
    Guid DocumentId
) : IRequest<CreateRenewalTaskResponseDto>;