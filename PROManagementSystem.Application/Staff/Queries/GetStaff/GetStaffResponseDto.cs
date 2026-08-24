using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Queries.GetStaff;

public sealed record GetStaffResponseDto(
    List<StaffListItemDto> Items,
    int TotalCount
);

public sealed record StaffListItemDto(
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