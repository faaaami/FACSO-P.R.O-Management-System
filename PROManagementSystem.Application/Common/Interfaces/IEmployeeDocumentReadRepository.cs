using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IEmployeeDocumentReadRepository
{
    Task<GetEmployeeDocumentsResponseDto> GetByEmployeeIdAsync(
        Guid employeeId,
        bool includeDeleted,
        CancellationToken cancellationToken);
}