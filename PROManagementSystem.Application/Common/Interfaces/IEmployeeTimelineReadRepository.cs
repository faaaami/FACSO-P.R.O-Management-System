using PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IEmployeeTimelineReadRepository
{
    Task<GetEmployeeTimelineResponseDto> GetTimelineAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}