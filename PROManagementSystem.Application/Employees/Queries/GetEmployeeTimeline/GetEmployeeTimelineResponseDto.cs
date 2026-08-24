namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;

public sealed record GetEmployeeTimelineResponseDto(
    Guid EmployeeId,
    List<EmployeeTimelineItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record EmployeeTimelineItemDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    string? Description,
    DateTime CreatedAt
);