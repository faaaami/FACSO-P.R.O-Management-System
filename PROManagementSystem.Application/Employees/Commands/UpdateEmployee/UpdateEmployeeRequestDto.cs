namespace PROManagementSystem.Application.Employees.Commands.UpdateEmployee;

public sealed record UpdateEmployeeRequestDto(
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive
);