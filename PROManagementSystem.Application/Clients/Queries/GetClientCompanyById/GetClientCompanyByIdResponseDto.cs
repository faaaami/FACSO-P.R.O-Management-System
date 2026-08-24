namespace PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;

public sealed record GetClientCompanyByIdResponseDto(
    Guid Id,
    string CompanyName,
    string? TradeLicenseNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Emirate,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);