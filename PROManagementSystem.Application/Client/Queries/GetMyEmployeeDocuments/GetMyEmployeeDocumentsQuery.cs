using MediatR;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Application.Client.Queries.GetMyEmployeeDocuments;

public sealed record GetMyEmployeeDocumentsQuery(
    Guid EmployeeId
) : IRequest<GetEmployeeDocumentsResponseDto>;