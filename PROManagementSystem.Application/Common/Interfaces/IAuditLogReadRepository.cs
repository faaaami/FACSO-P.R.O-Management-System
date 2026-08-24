using PROManagementSystem.Application.AuditLogs.Queries.GetAuditLog;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IAuditLogReadRepository
{
    Task<GetAuditLogResponseDto> GetListAsync(
        string? entityType,
        Guid? entityId,
        Guid? userId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
