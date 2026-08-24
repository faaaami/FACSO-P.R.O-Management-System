namespace PROManagementSystem.Application.Clients.Commands.UpdateClientEntity;

public sealed record UpdateClientEntityResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string EntityName,
    string? TradeLicenseNumber,
    string? Emirate,
    bool IsActive,
    DateTime UpdatedAt
);