using MediatR;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Application.Client.Queries.GetMyEmployees;

public sealed record GetMyEmployeesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? EntityId = null
) : IRequest<GetEmployeesResponseDto>;