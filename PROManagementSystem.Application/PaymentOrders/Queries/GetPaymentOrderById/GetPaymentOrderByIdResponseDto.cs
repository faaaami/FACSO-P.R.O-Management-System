namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrderById;

public sealed record GetPaymentOrderByIdResponseDto(
    Guid Id,
    int InvoiceType,
    Guid? RetainerInvoiceId,
    Guid? ServiceFeeInvoiceId,
    Guid ClientCompanyId,
    string ClientCompanyName,
    decimal Amount,
    string Currency,
    int Status,
    string? RazorpayOrderId,
    string? RazorpayPaymentId,
    DateTime? PaidAt,
    DateTime? FailedAt,
    DateTime? CancelledAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal? CapturedAmount,
    string? CapturedCurrency,
    string? ReviewReason
);
