using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IEmployeeReadRepository
{
    Task<GetEmployeesResponseDto> GetPagedAsync(
        Guid? clientId,
        Guid? entityId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<GetEmployeeByIdResponseDto?> GetByIdAsync(
    Guid employeeId,
    bool includeDeleted,
    CancellationToken cancellationToken);
}