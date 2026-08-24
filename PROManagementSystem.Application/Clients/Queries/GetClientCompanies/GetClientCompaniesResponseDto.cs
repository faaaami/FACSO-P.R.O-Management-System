namespace PROManagementSystem.Application.Clients.Queries.GetClientCompanies;

public sealed record ClientCompanyListItemDto(
    Guid Id,
    string CompanyName,
    string? TradeLicenseNumber,
    string? Phone,
    string? Email,
    string? Emirate,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt
);

public sealed record GetClientCompaniesResponseDto(
    IReadOnlyList<ClientCompanyListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);