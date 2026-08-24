namespace PROManagementSystem.Application.Staff.Commands.DeactivateStaff;

public sealed record DeactivateStaffResponseDto(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    DateTime UpdatedAt
);