using MediatR;

namespace PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

public sealed record GetExpiringEmployeesQuery(
    int Days,
    int Page,
    int PageSize,
    bool IncludeExpired
) : IRequest<GetExpiringEmployeesResponseDto>;