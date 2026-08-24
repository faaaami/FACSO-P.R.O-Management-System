using MediatR;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

public sealed record GetEmployeeDocumentsQuery(
    Guid EmployeeId,
    bool IncludeDeleted = false
) : IRequest<GetEmployeeDocumentsResponseDto>;