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
}

public sealed record RazorpayOrderResult(
    string OrderId,
    int Amount,
    string Currency,
    string Receipt,
    string Status
);
