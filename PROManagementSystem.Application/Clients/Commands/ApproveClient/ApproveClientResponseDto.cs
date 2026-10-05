using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.ApproveClient;

public sealed record ApproveClientResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool IsApproved,
    bool EmailVerified,
    DateTime UpdatedAt
);