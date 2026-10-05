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

    /// <summary>
    /// Closes a live gateway order so it can no longer be paid.
    /// </summary>
    /// <remarks>
    /// A closed gateway order can no longer capture a payment, which is what
    /// lets local cancellation and invoice voiding treat the order as truly
    /// terminal instead of leaving a payable order behind.
    /// <para>
    /// Closing an order that is already closed is treated as SUCCESS and
    /// reported through <c>WasAlreadyClosed</c>, because the desired end state
    /// already holds. Any other gateway failure throws
    /// <see cref="RazorpayCloseOrderException"/> so callers can roll back
    /// their local transaction and leave the order in its previous state
    /// rather than recording a cancellation the gateway never accepted.
    /// </para>
    /// <para>
    /// Only the CLOSING of the gateway order is decided here. Razorpay allows
    /// closing an order that has already been paid, so callers must still
    /// verify the local order is unsettled before requesting a close.
    /// </para>
    /// </remarks>
    /// <exception cref="RazorpayCloseOrderException">
    /// The gateway refused or failed to close the order.
    /// </exception>
    Task<RazorpayCloseOrderResult> CloseOrderAsync(
        string razorpayOrderId,
        CancellationToken cancellationToken = default);

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

/// <summary>
/// Outcome of a successful gateway order close.
/// </summary>
/// <param name="OrderId">The gateway order that was addressed.</param>
/// <param name="Status">Status reported by the gateway after the close.</param>
/// <param name="WasAlreadyClosed">
/// True when the gateway reported the order as already closed, so this call
/// was a no-op. A caller retrying after a lost response lands here and can
/// safely commit its local cancellation.
/// </param>
public sealed record RazorpayCloseOrderResult(
    string OrderId,
    string Status,
    bool WasAlreadyClosed
);

/// <summary>
/// Raised when the gateway could not close an order. Carries the gateway
/// order id and its reported status so callers can surface an actionable
/// message and decide whether the failure is retryable.
/// </summary>
public sealed class RazorpayCloseOrderException : Exception
{
    public RazorpayCloseOrderException(
        string message,
        string razorpayOrderId,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RazorpayOrderId = razorpayOrderId;
    }

    public string RazorpayOrderId { get; }
}
