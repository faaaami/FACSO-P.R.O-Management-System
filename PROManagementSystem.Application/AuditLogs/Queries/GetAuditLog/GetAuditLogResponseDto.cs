namespace PROManagementSystem.Application.AuditLogs.Queries.GetAuditLog;

public sealed record GetAuditLogResponseDto(
    List<AuditLogItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record AuditLogItemDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityType,
    Guid EntityId,
    Guid? EmployeeId,
    string? Description,
    string? Metadata,
    DateTime CreatedAt
);
