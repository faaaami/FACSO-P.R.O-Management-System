namespace PROManagementSystem.Domain.Enums;

public enum PaymentStatus
{
    Created = 1,
    Pending = 2,
    Paid = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6,

    /// <summary>
    /// The gateway captured money that could not be applied to the invoice
    /// (already settled or voided, amount or currency mismatch, invoice
    /// missing). The invoice is left untouched and an admin must refund or
    /// allocate the payment.
    /// </summary>
    PaidRequiresReview = 7
}
