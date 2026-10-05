using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

public sealed record CompletePaymentFromWebhookResponseDto(
    Guid PaymentOrderId,
    string RazorpayOrderId,
    PaymentStatus Status,
    DateTime? PaidAt,
    string EventId,
    bool Completed
);
