namespace PROManagementSystem.Application.PaymentOrders.Queries.GetPaymentOrders;

public sealed record GetPaymentOrdersResponseDto(
    List<PaymentOrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record PaymentOrderDto(
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
