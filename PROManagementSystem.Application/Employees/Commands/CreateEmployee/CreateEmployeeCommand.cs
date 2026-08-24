using MediatR;

namespace PROManagementSystem.Application.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(
    Guid ClientEntityId,
    string FullName,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Nationality,
    string? JobTitle,
    DateTime HireDate
) : IRequest<CreateEmployeeResponseDto>;