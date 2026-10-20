using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>Checklist line: a document needed for the job and whether it was received.</summary>
public sealed class ServiceJobDocument : BaseEntity
{
    public Guid ServiceJobId { get; set; }

    public ServiceJob? ServiceJob { get; set; }

    public int Order { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;

    public bool IsReceived { get; set; }

    public DateTime? ReceivedAt { get; set; }
}
