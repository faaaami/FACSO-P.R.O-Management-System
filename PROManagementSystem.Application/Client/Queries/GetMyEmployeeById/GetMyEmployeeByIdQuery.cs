using MediatR;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

namespace PROManagementSystem.Application.Client.Queries.GetMyEmployeeById;

public sealed record GetMyEmployeeByIdQuery(
    Guid EmployeeId
) : IRequest<GetEmployeeByIdResponseDto?>;