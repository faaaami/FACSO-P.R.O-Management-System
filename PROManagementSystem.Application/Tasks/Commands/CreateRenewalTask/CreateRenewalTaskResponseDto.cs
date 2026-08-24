using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.CreateRenewalTask;

public sealed record CreateRenewalTaskResponseDto(
    Guid Id,
    Guid DocumentId,
    Guid ClientCompanyId,
    Guid? AssignedStaffId,
    RenewalTaskStatus Status,
    string? BlockedReason,
    DateTime? BlockedSince,
    DateTime? CompletedAt,
    DateTime CreatedAt
);