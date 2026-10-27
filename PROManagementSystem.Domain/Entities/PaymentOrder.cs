using PROMangementSystem.Domain.Common;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Domain.Entities;

public sealed class PaymentOrder : BaseEntity
{
    public PaymentInvoiceType InvoiceType { get; set; }

    public Guid? RetainerInvoiceId { get; set; }

    public RetainerInvoice? RetainerInvoice { get; set; }

    public Guid? ServiceFeeInvoiceId { get; set; }

    public ServiceFeeInvoice? ServiceFeeInvoice { get; set; }

    public Guid ClientCompanyId { get; set; }

    public ClientCompany? ClientCompany { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "AED";

    public PaymentStatus Status { get; set; }
        = PaymentStatus.Created;

    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string? RazorpaySignature { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? FailedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    /// <summary>Amount the gateway actually captured, in major units.</summary>
    public decimal? CapturedAmount { get; set; }

    /// <summary>Currency the gateway actually captured.</summary>
    public string? CapturedCurrency { get; set; }

    /// <summary>Why a captured payment needs review (PaidRequiresReview).</summary>
    public string? ReviewReason { get; set; }

    public string? Notes { get; set; }
}
