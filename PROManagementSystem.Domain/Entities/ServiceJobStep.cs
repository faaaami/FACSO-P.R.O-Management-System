using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public sealed class ServiceJobStep : BaseEntity
{
    public Guid ServiceJobId { get; set; }

    public ServiceJob? ServiceJob { get; set; }

    public int Order { get; set; }

    public string Name { get; set; } = string.Empty;

    public ServiceJobStepStatus Status { get; set; } = ServiceJobStepStatus.Pending;

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? CompletedByUserId { get; set; }
}
