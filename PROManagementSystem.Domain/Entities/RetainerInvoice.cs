using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public sealed class RetainerInvoice : BaseEntity
{
    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public Guid ServiceContractId { get; set; }

    public ServiceContract? ServiceContract { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? PeriodStart { get; set; }

    public DateTime? PeriodEnd { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "AED";

    public RetainerInvoiceStatus Status { get; set; }
        = RetainerInvoiceStatus.Pending;

    public string? Notes { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? VoidedAt { get; set; }

    public string? VoidReason { get; set; }
}
