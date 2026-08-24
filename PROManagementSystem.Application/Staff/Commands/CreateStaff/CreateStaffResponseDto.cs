using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Commands.CreateStaff;

public sealed record CreateStaffResponseDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool IsApproved,
    bool EmailVerified,
    DateTime CreatedAt
);