using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Auth.Commands.Register;

public sealed record RegisterResponseDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsApproved,
    bool IsActive
);