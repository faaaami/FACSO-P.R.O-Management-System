namespace PROManagementSystem.Application.Employees.Commands.UpdateEmployee;

public sealed record UpdateEmployeeResponseDto(
    Guid Id,
    Guid ClientEntityId,
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive,
    DateTime UpdatedAt
);