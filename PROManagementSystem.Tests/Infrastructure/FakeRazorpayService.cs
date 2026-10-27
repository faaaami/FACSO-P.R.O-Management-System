using System.Security.Cryptography;
using System.Text;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Test-only replacement for the real Razorpay HTTP client. Returns a
/// deterministic order result so client create-order endpoints can be tested
/// without making live network calls, and verifies webhook signatures using a
/// faithful HMAC-SHA256 implementation over the constant test WebhookSecret.
/// </summary>
public sealed class FakeRazorpayService : IRazorpayService
{
    public const string FakeKeyId = "rzp_test_FakeKeyId";
    public const string FakeOrderId = "rzp_test_order_stage2";
    public const string FakePaymentId = "rzp_test_payment_stage2";
    public const string FakeSignature = "fake-razorpay-signature";
    public const string FakeWebhookSecret = "test-razorpay-webhook-secret-0123456789";

    private static int _createOrderCallCount;
    private static int _lastRequestedAmountInSmallestUnit;
    private static string? _lastRequestedCurrency;

    /// <summary>
    /// Number of gateway create-order calls made since the last reset. The
    /// counter is static because <c>IRazorpayService</c> is registered
    /// scoped, so each request receives a fresh instance and an instance
    /// counter would always read zero across requests.
    /// </summary>
    public static int CreateOrderCallCount
        => Volatile.Read(ref _createOrderCallCount);

    /// <summary>
    /// Amount (in the gateway's minor unit) passed to the most recent
    /// create-order call.
    /// </summary>
    public static int LastRequestedAmountInSmallestUnit
        => Volatile.Read(ref _lastRequestedAmountInSmallestUnit);

    /// <summary>
    /// Currency passed to the most recent create-order call.
    /// </summary>
    public static string? LastRequestedCurrency
        => Volatile.Read(ref _lastRequestedCurrency);

    /// <summary>
    /// Clears the recorded gateway interaction. Called from the shared
    /// integration-test base before every test so the counter and the
    /// deterministic order-id sequence never leak between tests.
    /// </summary>
    public static void ResetCreateOrderTracking()
    {
        Interlocked.Exchange(ref _createOrderCallCount, 0);
        Volatile.Write(ref _lastRequestedAmountInSmallestUnit, 0);
        Volatile.Write(ref _lastRequestedCurrency, null);
    }

    public string KeyId => FakeKeyId;

    public Task<RazorpayOrderResult> CreateOrderAsync(
        int amountInSmallestUnit,
        string currency,
        string receipt,
        CancellationToken cancellationToken = default)
    {
        var callNumber =
            Interlocked.Increment(ref _createOrderCallCount);

        Volatile.Write(
            ref _lastRequestedAmountInSmallestUnit,
            amountInSmallestUnit);

        Volatile.Write(ref _lastRequestedCurrency, currency);

        // The FIRST call of a test returns the exact FakeOrderId so existing
        // single-gateway-order assertions keep working. Later calls get
        // distinct ids, which is what lets a test detect an orphaned second
        // gateway order.
        var orderId = callNumber == 1
            ? FakeOrderId
            : $"{FakeOrderId}_{callNumber}";

        var order = new RazorpayOrderResult(
            orderId,
            amountInSmallestUnit,
            currency,
            receipt,
            "created");

        return Task.FromResult(order);
    }

    public Task<bool> VerifyPaymentAsync(
        string razorpayOrderId,
        string razorpayPaymentId,
        string razorpaySignature,
        CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> VerifyWebhookSignatureAsync(
        string rawBody,
        string signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return Task.FromResult(false);
        }

        try
        {
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(FakeWebhookSecret));

            var hash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(rawBody));

            var expected = Convert
                .ToHexString(hash)
                .ToLowerInvariant();

            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature));

            return Task.FromResult(isValid);
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }
    }
}
