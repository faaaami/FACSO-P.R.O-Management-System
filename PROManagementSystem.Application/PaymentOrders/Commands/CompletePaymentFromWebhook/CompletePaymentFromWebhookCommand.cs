using MediatR;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

/// <summary>
/// Completes a payment in response to a verified Razorpay webhook.
/// The invoice is NEVER selected from arbitrary request data - it is always
/// derived from the persisted <c>PaymentOrder</c> located by
/// <see cref="RazorpayOrderId"/>. All fields come from the verified payload
/// or headers; none selects an invoice directly.
/// </summary>
public sealed record CompletePaymentFromWebhookCommand(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    int AmountInSmallestUnit,
    string Currency,
    string PaymentEventName,
    string PaymentStatus,
    string WebhookEventId,
    string RazorpaySignature
) : IRequest<CompletePaymentFromWebhookResponseDto>;
