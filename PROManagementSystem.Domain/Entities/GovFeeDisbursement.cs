using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public sealed class GovFeeDisbursement : BaseEntity
{
    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public Guid? RenewalTaskId { get; set; }

    public RenewalTask? RenewalTask { get; set; }

    public Guid? ServiceJobId { get; set; }

    public ServiceJob? ServiceJob { get; set; }

    public string FeeDescription { get; set; } = string.Empty;

    public string? GovernmentReference { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "AED";

    public GovFeeDisbursementStatus Status { get; set; }
        = GovFeeDisbursementStatus.PaidByFirm;

    public DateTime PaidByFirmAt { get; set; }

    public DateTime? InvoicedAt { get; set; }

    public DateTime? ReimbursedAt { get; set; }

    public string? Notes { get; set; }
}
