using PROManagementSystem.Domain.Enums;
using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

public class RenewalTask : BaseEntity
{
    // The document that triggered this renewal task.
    public Guid DocumentId { get; set; }

    public Document? Document { get; set; }

    // Company owning the renewal.
    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    // Agent responsible for the task.
    public Guid? AssignedStaffId { get; set; }

    public User? AssignedStaff { get; set; }

    public RenewalTaskStatus Status { get; set; }
        = RenewalTaskStatus.Submitted;

    public string? BlockedReason { get; set; }

    public DateTime? BlockedSince { get; set; }

    public DateTime? CompletedAt { get; set; }

    public decimal? ServiceFeeAmount { get; set; }

    public ICollection<RenewalStepLog> StepLogs { get; set; }
        = new List<RenewalStepLog>();

    public ICollection<RenewalTaskHistory> History { get; set; }
        = new List<RenewalTaskHistory>();
}