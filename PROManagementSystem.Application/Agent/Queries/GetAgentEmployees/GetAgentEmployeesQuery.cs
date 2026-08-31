using MediatR;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentEmployees;

public sealed record GetAgentEmployeesQuery(
    int Page = 1,
    int PageSize = 20
) : IRequest<GetEmployeesResponseDto>;
