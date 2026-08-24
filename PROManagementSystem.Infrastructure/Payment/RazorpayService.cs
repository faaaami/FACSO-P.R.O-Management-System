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
}
