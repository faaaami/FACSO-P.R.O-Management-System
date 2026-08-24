using PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IEmployeeExpiryReadRepository
{
    Task<GetExpiringEmployeesResponseDto> GetExpiringAsync(
        int days,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken);
}