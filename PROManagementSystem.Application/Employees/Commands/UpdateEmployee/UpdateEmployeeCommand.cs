using MediatR;

namespace PROManagementSystem.Application.Employees.Commands.UpdateEmployee;

public sealed record UpdateEmployeeCommand(
    Guid EmployeeId,
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive
) : IRequest<UpdateEmployeeResponseDto>;