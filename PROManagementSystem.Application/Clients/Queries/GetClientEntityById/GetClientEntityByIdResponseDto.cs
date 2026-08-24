namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityById;

public sealed record GetClientEntityByIdResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string EntityName,
    string? TradeLicenseNumber,
    string? Emirate,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);