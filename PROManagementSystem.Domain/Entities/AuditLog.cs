using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? Description { get; set; }

    public string? Metadata { get; set; }
}