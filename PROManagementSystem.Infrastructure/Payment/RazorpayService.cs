using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using Razorpay.Api;

namespace PROManagementSystem.Infrastructure.Payment;

public sealed class RazorpayOptions
{
    public string KeyId { get; set; } = string.Empty;

    public string KeySecret { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;
}

public sealed class RazorpayService : IRazorpayService
{
    private readonly HttpClient _httpClient;
    private readonly RazorpayOptions _options;
    private readonly ILogger<RazorpayService> _logger;

    public RazorpayService(
        HttpClient httpClient,
        IOptions<RazorpayOptions> options,
        ILogger<RazorpayService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string KeyId => _options.KeyId;

    public Task<RazorpayOrderResult> CreateOrderAsync(
        int amountInSmallestUnit,
        string currency,
        string receipt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating Razorpay order: amount={Amount}, currency={Currency}, receipt={Receipt}",
            amountInSmallestUnit,
            currency,
            receipt);

        var client =
            new RazorpayClient(
                _options.KeyId,
                _options.KeySecret);

        var request =
            new Dictionary<string, object>
            {
                { "amount", amountInSmallestUnit },
                { "currency", currency },
                { "receipt", receipt }
            };

        var order =
            client.Order.Create(request);

        string orderId =
            ((string)order["id"]) ?? string.Empty;

        string amountRaw =
            ((string)order["amount"]) ?? string.Empty;
        int orderAmount =
            int.TryParse(amountRaw, out var parsed)
                ? parsed
                : amountInSmallestUnit;

        string orderCurrency =
            ((string)order["currency"]) ?? currency;
        string orderReceipt =
            ((string)order["receipt"]) ?? receipt;
        string orderStatus =
            ((string)order["status"]) ?? "created";

        _logger.LogInformation(
            "Razorpay order created: orderId={OrderId}, status={Status}",
            orderId,
            orderStatus);

        return Task.FromResult(
            new RazorpayOrderResult(
                orderId,
                orderAmount,
                orderCurrency,
                orderReceipt,
                orderStatus));
    }

    /// <summary>
    /// Closes a live gateway order.
    /// </summary>
    /// <remarks>
    /// Implemented as read-then-act rather than a blind <c>Order.Edit</c>:
    /// the current gateway status is fetched first so an order that is already
    /// closed is reported as success without issuing a write (a repeat close is
    /// an error at the gateway, not an idempotent no-op), and an order that is
    /// already <c>paid</c> is refused instead of being reported as closed.
    /// <para>
    /// The Razorpay SDK exposes <c>Order.Edit</c> with a single
    /// <c>Dictionary&lt;string, object&gt;</c> overload and resolves the
    /// target order from the <c>id</c> entry, so the close itself is
    /// <c>Edit({ id, status: "closed" })</c>.
    /// </para>
    /// <para>
    /// This mirrors <see cref="CreateOrderAsync"/> in using the synchronous
    /// SDK client rather than the injected <c>HttpClient</c>.
    /// </para>
    /// </remarks>
    public Task<RazorpayCloseOrderResult> CloseOrderAsync(
        string razorpayOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(razorpayOrderId))
        {
            throw new RazorpayCloseOrderException(
                "A gateway order id is required to close a Razorpay order.",
                razorpayOrderId ?? string.Empty);
        }

        _logger.LogInformation(
            "Closing Razorpay order: orderId={OrderId}",
            razorpayOrderId);

        try
        {
            var client =
                new RazorpayClient(
                    _options.KeyId,
                    _options.KeySecret);

            var current =
                client.Order.Fetch(
                    razorpayOrderId,
                    new Dictionary<string, object>());

            var currentStatus =
                ((string?)current?["status"]) ?? string.Empty;

            // Already closed: the desired end state holds, so succeed without
            // a second write. This is the retry-after-lost-response path.
            if (string.Equals(
                    currentStatus,
                    "closed",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Razorpay order was already closed: orderId={OrderId}",
                    razorpayOrderId);

                return Task.FromResult(
                    new RazorpayCloseOrderResult(
                        razorpayOrderId,
                        "closed",
                        WasAlreadyClosed: true));
            }

            // Paid money is never closable. Callers guard against this
            // locally, so reaching it means the local order and the gateway
            // disagree and the safe answer is to refuse.
            if (string.Equals(
                    currentStatus,
                    "paid",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Refusing to close an already-paid Razorpay order: " +
                    "orderId={OrderId}",
                    razorpayOrderId);

                throw new RazorpayCloseOrderException(
                    $"Order '{razorpayOrderId}' has already been paid and " +
                    "cannot be closed.",
                    razorpayOrderId);
            }

            var request =
                new Dictionary<string, object>
                {
                    { "id", razorpayOrderId },
                    { "status", "closed" }
                };

            var closed = client.Order.Edit(request);

            var status =
                ((string?)closed?["status"]) ?? "closed";

            _logger.LogInformation(
                "Razorpay order closed: orderId={OrderId}, status={Status}",
                razorpayOrderId,
                status);

            return Task.FromResult(
                new RazorpayCloseOrderResult(
                    razorpayOrderId,
                    status,
                    WasAlreadyClosed: false));
        }
        catch (RazorpayCloseOrderException)
        {
            // Already a domain-level refusal; do not rewrap.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to close Razorpay order: orderId={OrderId}",
                razorpayOrderId);

            throw new RazorpayCloseOrderException(
                $"The payment provider could not close order " +
                $"'{razorpayOrderId}'. The order has not been cancelled.",
                razorpayOrderId,
                ex);
        }
    }

    public Task<bool> VerifyPaymentAsync(
        string razorpayOrderId,
        string razorpayPaymentId,
        string razorpaySignature,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Verifying Razorpay payment: orderId={OrderId}, paymentId={PaymentId}",
            razorpayOrderId,
            razorpayPaymentId);

        try
        {
            var payload =
                $"{razorpayOrderId}|{razorpayPaymentId}";

            using var hmac =
                new HMACSHA256(
                    Encoding.UTF8.GetBytes(
                        _options.KeySecret));

            var hash =
                hmac.ComputeHash(
                    Encoding.UTF8.GetBytes(payload));

            var expectedSignature =
                Convert
                    .ToHexString(hash)
                    .ToLowerInvariant();

            var isValid =
                CryptographicOperations
                    .FixedTimeEquals(
                        Encoding.UTF8
                            .GetBytes(expectedSignature),
                        Encoding.UTF8
                            .GetBytes(
                                razorpaySignature));

            _logger.LogInformation(
                "Razorpay signature verification: isValid={IsValid}",
                isValid);

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Razorpay signature verification failed: isValid=False");

            return Task.FromResult(false);
        }
    }

    public Task<bool> VerifyWebhookSignatureAsync(
        string rawBody,
        string signature,
        CancellationToken cancellationToken = default)
    {
        var webhookSecret = _options.WebhookSecret;

        if (string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogWarning(
                "Razorpay webhook signature verification rejected: " +
                "missing signature, body or webhook secret.");
            return Task.FromResult(false);
        }

        try
        {
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(webhookSecret));

            var hash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(rawBody));

            var expectedSignature =
                Convert.ToHexString(hash).ToLowerInvariant();

            var expectedBytes =
                Convert.FromHexString(expectedSignature);

            var providedBytes =
                TryFromHex(signature);

            if (providedBytes is null ||
                expectedBytes.Length != providedBytes.Length)
            {
                return Task.FromResult(false);
            }

            var isValid = CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                providedBytes);

            _logger.LogInformation(
                "Razorpay webhook signature verification: " +
                "isValid={IsValid}",
                isValid);

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Razorpay webhook signature verification failed: " +
                "isValid=False");
            return Task.FromResult(false);
        }
    }

    private static byte[]? TryFromHex(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
