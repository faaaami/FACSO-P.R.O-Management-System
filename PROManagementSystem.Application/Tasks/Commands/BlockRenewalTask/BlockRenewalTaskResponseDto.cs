using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;

public sealed record BlockRenewalTaskResponseDto(
    Guid TaskId,
    RenewalTaskStatus Status,
    string BlockedReason,
    DateTime BlockedSince,
    DateTime UpdatedAt
);