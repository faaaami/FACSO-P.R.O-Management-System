using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.AssignRenewalTask;

public sealed record AssignRenewalTaskResponseDto(
    Guid TaskId,
    Guid StaffId,
    string StaffName,
    RenewalTaskStatus Status,
    DateTime UpdatedAt
);