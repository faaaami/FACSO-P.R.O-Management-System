using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;

public sealed record UpdateRenewalTaskStatusCommand(
    Guid TaskId,
    string Status,
    string? Note,
    DateTime? RenewedExpiryDate
) : IRequest<UpdateRenewalTaskStatusResponseDto>;
