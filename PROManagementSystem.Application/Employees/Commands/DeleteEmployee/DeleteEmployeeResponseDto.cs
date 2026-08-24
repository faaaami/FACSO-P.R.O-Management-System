namespace PROManagementSystem.Application.Employees.Commands.DeleteEmployee;

public sealed record DeleteEmployeeResponseDto(
    Guid Id,
    bool IsDeleted,
    bool IsActive,
    DateTime DeletedAt
);