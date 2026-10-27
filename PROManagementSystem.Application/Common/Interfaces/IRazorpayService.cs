namespace PROManagementSystem.Application.Common.Interfaces;

public interface IRazorpayService
{
    string KeyId { get; }

    Task<RazorpayOrderResult> CreateOrderAsync(
        int amountInSmallestUnit,
        string currency,
        string receipt,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyPaymentAsync(
        string razorpayOrderId,
        string razorpayPaymentId,
        string razorpaySignature,
        CancellationToken cancellationToken = default);

    // There is deliberately no "close order" operation: the Razorpay Orders
    // API cannot close or cancel an order (PATCH /v1/orders/:id accepts only
    // `notes`). Orders are retired locally, and a payment captured on a
    // retired order is caught by the webhook as PaidRequiresReview.

    /// <summary>
    /// Verifies an inbound Razorpay webhook signature. This uses the webhook
    /// scheme (HMAC-SHA256 over the EXACT raw request body keyed with the
    /// WebhookSecret) and must NOT be confused with
    /// <see cref="VerifyPaymentAsync"/>, which uses the
    /// orderId|paymentId + KeySecret checkout scheme.
    /// The signature is treated as a hex HMAC digest (Razorpay webhook
    /// convention) and compared constant-time.
    /// </summary>
    Task<bool> VerifyWebhookSignatureAsync(
        string rawBody,
        string signature,
        CancellationToken cancellationToken = default);
}

public sealed record RazorpayOrderResult(
    string OrderId,
    int Amount,
    string Currency,
    string Receipt,
    string Status
);
