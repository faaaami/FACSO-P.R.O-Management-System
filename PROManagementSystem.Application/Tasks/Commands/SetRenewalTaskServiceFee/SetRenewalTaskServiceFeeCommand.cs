using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.SetRenewalTaskServiceFee;

public sealed record SetRenewalTaskServiceFeeCommand(
    Guid TaskId,
    decimal Amount
) : IRequest<SetRenewalTaskServiceFeeResponseDto>;
