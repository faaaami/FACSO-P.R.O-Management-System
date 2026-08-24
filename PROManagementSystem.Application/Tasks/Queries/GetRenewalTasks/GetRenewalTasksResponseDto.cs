using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;

public sealed record GetRenewalTasksResponseDto(
    List<RenewalTaskListItemDto> Items,
    int TotalCount
);

public sealed record RenewalTaskListItemDto(
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