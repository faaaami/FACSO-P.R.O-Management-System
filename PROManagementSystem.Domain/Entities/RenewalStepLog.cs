using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class RenewalStepLog : BaseEntity
{
    public Guid RenewalTaskId { get; set; }

    public RenewalTask? RenewalTask { get; set; }

    public string StepName { get; set; } = string.Empty;

    public Guid CompletedBy { get; set; }

    public User? CompletedByUser { get; set; }

    public DateTime CompletedAt { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? ProofFileUrl { get; set; }
}