using MediatR;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

public sealed record GetEmployeeByIdQuery(
    Guid EmployeeId,
    bool IncludeDeleted = false
) : IRequest<GetEmployeeByIdResponseDto?>;