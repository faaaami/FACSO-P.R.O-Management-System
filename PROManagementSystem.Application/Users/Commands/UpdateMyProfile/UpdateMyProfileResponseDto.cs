using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Users.Commands.UpdateMyProfile;

public sealed record UpdateMyProfileResponseDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool EmailVerified,
    Guid? ClientCompanyId
);