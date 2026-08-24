namespace PROManagementSystem.Application.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeResponseDto(
    Guid Id,
    Guid ClientEntityId,
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive
);