using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;

public sealed record UpdateRenewalTaskStatusResponseDto(
    Guid TaskId,
    RenewalTaskStatus PreviousStatus,
    RenewalTaskStatus Status,
    DateTime UpdatedAt
);
