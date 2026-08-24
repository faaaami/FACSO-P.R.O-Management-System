using MediatR;

namespace PROManagementSystem.Application.AuditLogs.Queries.GetAuditLog;

public sealed record GetAuditLogQuery(
    string? EntityType,
    Guid? EntityId,
    Guid? UserId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetAuditLogResponseDto>;
