namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntity;

public sealed record CreateClientEntityResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string EntityName,
    string? TradeLicenseNumber,
    string? Emirate,
    bool IsActive
);