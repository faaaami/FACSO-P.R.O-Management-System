namespace PROManagementSystem.Application.Clients.Queries.GetClientContacts;

public sealed record GetClientContactListItemDto(
    Guid Id,
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    bool IsActive,
    bool IsApproved,
    bool IsDeleted,
    DateTime CreatedAt
);

public sealed record GetClientContactsResponseDto(
    IReadOnlyList<GetClientContactListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);