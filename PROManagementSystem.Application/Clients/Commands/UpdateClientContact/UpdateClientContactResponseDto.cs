using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientContact;

public sealed record UpdateClientContactResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone,
    UserRole Role,
    bool IsActive,
    bool IsApproved,
    DateTime UpdatedAt
);