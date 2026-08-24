namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

public sealed record GetEmployeeByIdResponseDto(
    Guid Id,
    Guid ClientEntityId,
    Guid ClientCompanyId,
    string EntityName,
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);