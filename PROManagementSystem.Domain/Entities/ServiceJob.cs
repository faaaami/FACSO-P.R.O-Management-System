using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>
/// A unit of work the agency performs for a customer: any service from the
/// catalogue (new visa, cancellation, typing, licence renewal, ...).
/// </summary>
public sealed class ServiceJob : BaseEntity
{
    /// <summary>Human readable, sequential, e.g. JOB-2026-000042.</summary>
    public string JobNumber { get; set; } = string.Empty;

    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public Guid ServiceDefinitionId { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }

    /// <summary>Person the service is for, when the service is per person.</summary>
    public Guid? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    /// <summary>Company / licence the service is for.</summary>
    public Guid? ClientEntityId { get; set; }

    public ClientEntity? ClientEntity { get; set; }

    /// <summary>Existing document being renewed / replaced / cancelled, if any.</summary>
    public Guid? DocumentId { get; set; }

    public Document? Document { get; set; }

    public Guid? ServiceRequestId { get; set; }

    public Guid? AssignedStaffId { get; set; }

    public User? AssignedStaff { get; set; }

    public ServiceJobStatus Status { get; set; } = ServiceJobStatus.New;

    public ServiceJobPriority Priority { get; set; } = ServiceJobPriority.Normal;

    public ServiceJobSource Source { get; set; } = ServiceJobSource.Staff;

    /// <summary>Agreed service fee (before VAT) after contract pricing / manual override.</summary>
    public decimal ServiceFee { get; set; }

    public bool ServiceFeeVatable { get; set; } = true;

    public decimal EstimatedGovernmentFee { get; set; }

    /// <summary>Government application / transaction number once filed.</summary>
    public string? ApplicationNumber { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? Notes { get; set; }

    public string? OnHoldReason { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public Guid CreatedByUserId { get; set; }

    public ICollection<ServiceJobStep> Steps { get; set; } = new List<ServiceJobStep>();

    public ICollection<ServiceJobDocument> Documents { get; set; } = new List<ServiceJobDocument>();

    public ICollection<ServiceJobActivity> Activities { get; set; } = new List<ServiceJobActivity>();
}
