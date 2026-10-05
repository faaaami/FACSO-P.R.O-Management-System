using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PROManagementSystem.Infrastructure.Payment;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// Unit tests for the Razorpay webhook signature verification (fail-closed).
/// These do not require a database, so they live outside the shared
/// integration-test collection.
/// </summary>
public class RazorpayWebhookSignatureUnitTests
{
    private static RazorpayService CreateService(string webhookSecret)
        => new(
            new HttpClient(),
            Options.Create(new RazorpayOptions { WebhookSecret = webhookSecret }),
            NullLogger<RazorpayService>.Instance);

    private static string HmacHex(string secret, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();
    }

    [Fact(DisplayName =
        "WH-S1: valid signature over exact raw body verifies true")]
    public async Task ValidSignature_Verified()
    {
        const string secret = "unit-test-webhook-secret";
        const string rawBody = "{\"event\":\"payment.captured\"}";
        var svc = CreateService(secret);

        var ok = await svc.VerifyWebhookSignatureAsync(rawBody, HmacHex(secret, rawBody));
        Assert.True(ok);
    }

    [Fact(DisplayName =
        "WH-S2: signature computed over a different body verifies false")]
    public async Task DifferentBody_Fails()
    {
        const string secret = "unit-test-webhook-secret";
        var svc = CreateService(secret);

        var ok = await svc.VerifyWebhookSignatureAsync(
            "{\"event\":\"payment.captured\"}",
            HmacHex(secret, "{\"event\":\"payment.failed\"}"));
        Assert.False(ok);
    }

    [Fact(DisplayName =
        "WH-S3: empty webhook secret fails closed (false)")]
    public async Task EmptySecret_FailsClosed()
    {
        var svc = CreateService(string.Empty);

        var ok = await svc.VerifyWebhookSignatureAsync(
            "{\"event\":\"payment.captured\"}",
            HmacHex("any-secret", "{\"event\":\"payment.captured\"}"));
        Assert.False(ok);
    }

    [Fact(DisplayName =
        "WH-S4: missing signature fails closed (false)")]
    public async Task MissingSignature_FailsClosed()
    {
        var svc = CreateService("unit-test-webhook-secret");
        var ok = await svc.VerifyWebhookSignatureAsync(
            "{\"event\":\"payment.captured\"}", string.Empty);
        Assert.False(ok);
    }

    [Fact(DisplayName =
        "WH-S5: non-hex garbage signature fails closed (false)")]
    public async Task GarbageSignature_FailsClosed()
    {
        var svc = CreateService("unit-test-webhook-secret");
        var ok = await svc.VerifyWebhookSignatureAsync(
            "{\"event\":\"payment.captured\"}", "not-a-hex-signature!!!");
        Assert.False(ok);
    }
}
