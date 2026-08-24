namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

public sealed record CreateClientContactResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    bool IsActive,
    bool IsApproved
);