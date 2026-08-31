using MediatR;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentEmployeeById;

public sealed record GetAgentEmployeeByIdQuery(
    Guid EmployeeId
) : IRequest<GetEmployeeByIdResponseDto?>;
