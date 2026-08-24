namespace PROManagementSystem.Application.Clients.Commands.CreateClientCompany;

public sealed record CreateClientCompanyResponseDto(
    Guid Id,
    string CompanyName,
    string? TradeLicenseNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Emirate,
    bool IsActive
);