namespace PROManagementSystem.Application.Clients.Queries.GetClientEntities;

public sealed record GetClientEntityListItemDto(
    Guid Id,
    Guid ClientCompanyId,
    string EntityName,
    string? TradeLicenseNumber,
    string? Emirate,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt
);

public sealed record GetClientEntitiesResponseDto(
    IReadOnlyList<GetClientEntityListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);