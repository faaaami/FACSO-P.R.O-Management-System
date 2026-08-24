namespace PROManagementSystem.Application.Clients.Commands.UpdateClientCompany;

public sealed record UpdateClientCompanyResponseDto(
    Guid Id,
    string CompanyName,
    string? TradeLicenseNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Emirate,
    bool IsActive,
    DateTime UpdatedAt
);