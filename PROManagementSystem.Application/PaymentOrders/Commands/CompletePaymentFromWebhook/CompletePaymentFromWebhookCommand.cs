using MediatR;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

/// <summary>
/// Applies a verified Razorpay webhook event. The invoice is NEVER selected
/// from request data - it is always derived from the persisted
/// <c>PaymentOrder</c> located by <see cref="RazorpayOrderId"/>. Payment
/// fields are optional because not every event type carries a payment
/// entity; events that cannot be applied are recorded and ignored.
/// </summary>
public sealed record CompletePaymentFromWebhookCommand(
    string? RazorpayOrderId,
    string? RazorpayPaymentId,
    int? AmountInSmallestUnit,
    string? Currency,
    string PaymentEventName,
    string? PaymentStatus,
    string WebhookEventId,
    string RazorpaySignature
) : IRequest<CompletePaymentFromWebhookResponseDto>;
