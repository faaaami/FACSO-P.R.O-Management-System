using MediatR;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployees;

public sealed record GetEmployeesQuery(
    Guid? ClientId,
    Guid? EntityId,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetEmployeesResponseDto>;