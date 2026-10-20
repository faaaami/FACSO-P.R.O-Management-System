using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Helpers;

public static class AuditLogExtensions
{
    /// <summary>Adds an audit row to the current unit of work (saved with it).</summary>
    public static void AddAudit(
        this IApplicationDbContext context,
        Guid? userId,
        string action,
        string entityType,
        Guid entityId,
        string description,
        Guid? employeeId = null)
    {
        var now = DateTime.UtcNow;
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EmployeeId = employeeId,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
