using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Commands.UpdateStaff;

public sealed record UpdateStaffResponseDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool IsApproved,
    bool EmailVerified,
    DateTime UpdatedAt
);