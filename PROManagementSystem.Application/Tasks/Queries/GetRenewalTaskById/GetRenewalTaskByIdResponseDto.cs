using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

public sealed record GetRenewalTaskByIdResponseDto(
    Guid Id,
    RenewalTaskStatus Status,
    Guid DocumentId,
    DocumentInfoDto Document,
    Guid ClientCompanyId,
    AssignedStaffInfoDto? AssignedStaff,
    string? BlockedReason,
    DateTime? BlockedSince,
    DateTime? CompletedAt,
    decimal? ServiceFeeAmount,
    int StepLogCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record DocumentInfoDto(
    Guid Id,
    DocumentType Type,
    string DocumentNumber,
    DateTime? IssueDate,
    DateTime? ExpiryDate,
    string? FileName
);

public sealed record AssignedStaffInfoDto(
    Guid Id,
    string FullName,
    string Email
);