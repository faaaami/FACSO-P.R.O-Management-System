using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Queries.GetStaffById;

public sealed record GetStaffByIdResponseDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool IsApproved,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime UpdatedAt
);