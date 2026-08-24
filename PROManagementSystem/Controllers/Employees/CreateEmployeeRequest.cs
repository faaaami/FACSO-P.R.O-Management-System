namespace PROManagementSystem.Api.Controllers.Employees;

public sealed record CreateEmployeeRequest(
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate
);