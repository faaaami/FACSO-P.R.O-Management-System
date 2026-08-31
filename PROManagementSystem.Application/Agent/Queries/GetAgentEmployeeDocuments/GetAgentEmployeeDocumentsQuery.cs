using MediatR;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentEmployeeDocuments;

public sealed record GetAgentEmployeeDocumentsQuery(
    Guid EmployeeId
) : IRequest<GetEmployeeDocumentsResponseDto>;
