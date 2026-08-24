using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

public sealed record GetRenewalTaskHistoryResponseDto(
    Guid TaskId,
    IReadOnlyList<RenewalTaskHistoryDto> Items,
    int TotalCount
);

public sealed record RenewalTaskHistoryDto(
    Guid Id,
    RenewalTaskStatus Status,
    Guid ChangedBy,
    DateTime ChangedAt,
    string? Note
);