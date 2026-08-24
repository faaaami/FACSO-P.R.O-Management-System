using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Users.Queries.GetMyProfile;

public sealed record GetMyProfileResponseDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool EmailVerified,
    Guid? ClientCompanyId
);