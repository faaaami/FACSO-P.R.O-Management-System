using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public sealed class ServiceFeeInvoice : BaseEntity
{
    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public Guid RenewalTaskId { get; set; }

    public RenewalTask? RenewalTask { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "AED";

    public ServiceFeeInvoiceStatus Status { get; set; }
        = ServiceFeeInvoiceStatus.Pending;

    public string? Description { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? VoidedAt { get; set; }

    public string? VoidReason { get; set; }
}
