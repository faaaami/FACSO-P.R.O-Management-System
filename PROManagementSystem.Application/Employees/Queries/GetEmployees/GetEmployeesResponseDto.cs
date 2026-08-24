namespace PROManagementSystem.Application.Employees.Queries.GetEmployees;

public sealed record GetEmployeeListItemDto(
    Guid Id,
    Guid ClientEntityId,
    string EntityName,
    string FullName,
    string? PassportNumber,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt
);

public sealed record GetEmployeesResponseDto(
    IReadOnlyList<GetEmployeeListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);