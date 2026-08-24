using MediatR;

namespace PROManagementSystem.Application.Employees.Commands.DeleteEmployee;

public sealed record DeleteEmployeeCommand(
    Guid EmployeeId
) : IRequest<DeleteEmployeeResponseDto>;