namespace PROManagementSystem.Application.Tasks.Commands.CancelRenewalTask;

public sealed record CancelRenewalTaskResponseDto(
    Guid TaskId,
    bool IsDeleted,
    DateTime DeletedAt
);