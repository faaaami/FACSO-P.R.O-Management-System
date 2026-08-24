using MediatR;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;

public sealed record GetEmployeeTimelineQuery(
    Guid EmployeeId,
    int Page,
    int PageSize
) : IRequest<GetEmployeeTimelineResponseDto>;