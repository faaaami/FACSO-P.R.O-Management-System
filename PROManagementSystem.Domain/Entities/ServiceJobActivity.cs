using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>Timeline entry of a job (status changes, assignments, notes).</summary>
public sealed class ServiceJobActivity : BaseEntity
{
    public Guid ServiceJobId { get; set; }

    public ServiceJob? ServiceJob { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public ServiceJobStatus? FromStatus { get; set; }

    public ServiceJobStatus? ToStatus { get; set; }

    public string? Note { get; set; }

    /// <summary>Internal notes are hidden from the client portal.</summary>
    public bool IsInternal { get; set; }
}
