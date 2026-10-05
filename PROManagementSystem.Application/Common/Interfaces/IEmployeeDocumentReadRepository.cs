using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IEmployeeDocumentReadRepository
{
    Task<GetEmployeeDocumentsResponseDto> GetByEmployeeIdAsync(
        Guid employeeId,
        bool includeDeleted,
        CancellationToken cancellationToken);
    Task<GetEmployeeDocumentsResponseDto> GetByEmployeeIdForCompanyAsync(
        Guid clientCompanyId,
        Guid employeeId,
        CancellationToken cancellationToken);
}