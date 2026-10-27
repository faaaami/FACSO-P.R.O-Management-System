using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// Review item H1: the Razorpay webhook must never reject a verified event.
/// Captured money that cannot be applied is recorded on the order as
/// PaidRequiresReview and admins are notified; payment.failed closes the
/// order; unknown events are acknowledged; event ids are idempotent through
/// the webhook_events table. Bad signatures are still refused.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RazorpayWebhookResilienceTests : IntegrationTestBase
{
    private const string WebhookUrl =
        "/api/v1/client/payments/webhook";

    private const string AlreadySettledMessage =
        "Payment received for an invoice that was already settled: refund or allocate.";

    public RazorpayWebhookResilienceTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static string Signature(string rawBody)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(FakeRazorpayService.FakeWebhookSecret));

        return Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();
    }

    private static string PaymentBody(
        string evt,
        string orderId,
        string paymentId,
        int amount = 250000,
        string currency = "AED",
        string status = "captured")
        => "{\"event\":\"" + evt + "\"," +
           "\"payload\":{\"payment\":{\"entity\":{" +
           "\"id\":\"" + paymentId + "\"," +
           "\"order_id\":\"" + orderId + "\"," +
           "\"amount\":" + amount + "," +
           "\"currency\":\"" + currency + "\"," +
           "\"status\":\"" + status + "\"}}}}";

    private Task<HttpResponseMessage> PostAsync(
        string rawBody,
        string eventId,
        string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Razorpay-Signature", signature ?? Signature(rawBody));
        request.Headers.Add("X-Razorpay-Event-Id", eventId);

        return ScopeClient.SendAsync(request);
    }

    private static async Task<string?> OutcomeAsync(HttpResponseMessage response)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return (string?)body["data"]?["outcome"];
    }

    private async Task<Guid> SeedRetainerOrderAsync(
        string razorpayOrderId,
        PaymentStatus status = PaymentStatus.Pending)
    {
        var id = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = id,
                InvoiceType = PaymentInvoiceType.RetainerInvoice,
                RetainerInvoiceId = TestDataSeeder.Ids.RetainerInvoiceA,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                Amount = 2500m,
                Currency = "AED",
                Status = status,
                RazorpayOrderId = razorpayOrderId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });

        return id;
    }

    private async Task<PaymentOrder> OrderAsync(Guid id)
    {
        PaymentOrder? order = null;
        await WithScopeDbContextAsync(db =>
            order = db.PaymentOrders.AsNoTracking().First(x => x.Id == id));
        return order!;
    }

    private async Task<RetainerInvoice> InvoiceAsync()
    {
        RetainerInvoice? invoice = null;
        await WithScopeDbContextAsync(db =>
            invoice = db.RetainerInvoices.AsNoTracking()
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA));
        return invoice!;
    }

    private async Task<int> AuditCountAsync(Guid orderId, string action)
    {
        var count = 0;
        await WithScopeDbContextAsync(db =>
            count = db.AuditLogs.Count(x => x.EntityId == orderId && x.Action == action));
        return count;
    }

    private async Task<List<Notification>> AdminNotificationsAsync()
    {
        List<Notification> list = new();
        await WithScopeDbContextAsync(db =>
            list = db.Notifications.AsNoTracking()
                .Where(x => x.UserId == TestDataSeeder.Ids.Admin)
                .ToList());
        return list;
    }

    private Task<HttpResponseMessage> AdminMarkRetainerPaidAsync()
    {
        return ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}/mark-paid",
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    // ==================================================================
    // Late payment after manual settlement
    // ==================================================================

    [Fact(DisplayName =
        "H1-1: a payment captured after the invoice was marked paid manually is recorded for review, not rejected")]
    public async Task LatePayment_AfterManualMarkPaid_RequiresReview()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_late");

        Assert.Equal(HttpStatusCode.OK, (await AdminMarkRetainerPaidAsync()).StatusCode);
        var invoiceAfterManual = await InvoiceAsync();

        var response = await PostAsync(
            PaymentBody("payment.captured", "order_h1_late", "pay_h1_late"),
            "evt_h1_late");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("RequiresReview", await OutcomeAsync(response));

        var order = await OrderAsync(orderId);
        Assert.Equal(PaymentStatus.PaidRequiresReview, order.Status);
        Assert.Equal("pay_h1_late", order.RazorpayPaymentId);
        Assert.Equal(2500m, order.CapturedAmount);
        Assert.Equal("AED", order.CapturedCurrency);
        Assert.NotNull(order.PaidAt);
        Assert.Contains("already Paid", order.ReviewReason);

        // The invoice is not touched again.
        var invoice = await InvoiceAsync();
        Assert.Equal(RetainerInvoiceStatus.Paid, invoice.Status);
        Assert.Equal(invoiceAfterManual.PaidAt, invoice.PaidAt);

        Assert.Equal(1, await AuditCountAsync(orderId, "PaymentRequiresReview"));
        Assert.Equal(0, await AuditCountAsync(orderId, "PaymentCompletedFromWebhook"));

        var notifications = await AdminNotificationsAsync();
        Assert.Single(notifications);
        Assert.Equal("Payment needs review", notifications[0].Title);
        Assert.StartsWith(AlreadySettledMessage, notifications[0].Message);
    }

    [Fact(DisplayName =
        "H1-2: a payment captured on a voided invoice is recorded for review and the invoice stays void")]
    public async Task CapturedOnVoidInvoice_RequiresReview()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_void");

        await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices.First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
            inv.Status = RetainerInvoiceStatus.Void;
            db.SaveChanges();
        });

        var response = await PostAsync(
            PaymentBody("payment.captured", "order_h1_void", "pay_h1_void"),
            "evt_h1_void");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.PaidRequiresReview, (await OrderAsync(orderId)).Status);
        Assert.Equal(RetainerInvoiceStatus.Void, (await InvoiceAsync()).Status);
        Assert.Single(await AdminNotificationsAsync());
    }

    [Fact(DisplayName =
        "H1-3: an amount mismatch is recorded for review and leaves the invoice pending")]
    public async Task AmountMismatch_RequiresReview()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_amt");

        var response = await PostAsync(
            PaymentBody("payment.captured", "order_h1_amt", "pay_h1_amt", amount: 100000),
            "evt_h1_amt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await OrderAsync(orderId);
        Assert.Equal(PaymentStatus.PaidRequiresReview, order.Status);
        Assert.Equal(1000m, order.CapturedAmount);
        Assert.Contains("does not match", order.ReviewReason);
        Assert.Equal(RetainerInvoiceStatus.Pending, (await InvoiceAsync()).Status);
    }

    [Fact(DisplayName =
        "H1-4: a capture for an unknown Razorpay order is acknowledged and admins are notified")]
    public async Task UnknownOrder_Acknowledged_AdminsNotified()
    {
        var response = await PostAsync(
            PaymentBody("payment.captured", "order_h1_nowhere", "pay_h1_nowhere"),
            "evt_h1_nowhere");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("RequiresReview", await OutcomeAsync(response));
        Assert.Single(await AdminNotificationsAsync());
    }

    // ==================================================================
    // payment.failed / other events
    // ==================================================================

    [Fact(DisplayName =
        "H1-5: payment.failed marks the order Failed and the client can start a new payment")]
    public async Task PaymentFailed_MarksOrderFailed()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_fail");

        var response = await PostAsync(
            PaymentBody("payment.failed", "order_h1_fail", "pay_h1_fail", status: "failed"),
            "evt_h1_fail");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", await OutcomeAsync(response));

        var order = await OrderAsync(orderId);
        Assert.Equal(PaymentStatus.Failed, order.Status);
        Assert.NotNull(order.FailedAt);
        Assert.Equal(1, await AuditCountAsync(orderId, "PaymentOrderFailed"));
        Assert.Equal(RetainerInvoiceStatus.Pending, (await InvoiceAsync()).Status);

        var retry = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/client/payments/create-order",
            new { invoiceType = "Retainer", invoiceId = TestDataSeeder.Ids.RetainerInvoiceA },
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact(DisplayName =
        "H1-6: a capture on a Failed order (retry in the same checkout) still settles the pending invoice")]
    public async Task CaptureAfterFailure_SettlesInvoice_AndCancelsNewerOpenOrder()
    {
        var failedId = await SeedRetainerOrderAsync("order_h1_retry", PaymentStatus.Failed);
        var openId = await SeedRetainerOrderAsync("order_h1_newer");

        var response = await PostAsync(
            PaymentBody("payment.captured", "order_h1_retry", "pay_h1_retry"),
            "evt_h1_retry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Completed", await OutcomeAsync(response));
        Assert.Equal(PaymentStatus.Paid, (await OrderAsync(failedId)).Status);
        Assert.Equal(RetainerInvoiceStatus.Paid, (await InvoiceAsync()).Status);

        // The other checkout for the same invoice is retired locally; a later
        // capture on it is held for review rather than applied twice.
        Assert.Equal(PaymentStatus.Cancelled, (await OrderAsync(openId)).Status);
    }

    [Theory(DisplayName =
        "H1-7: event types the system does not act on are acknowledged with 200 and change nothing")]
    [InlineData("{\"event\":\"refund.processed\",\"payload\":{\"refund\":{\"entity\":{\"id\":\"rfnd_1\"}}}}")]
    [InlineData("{\"event\":\"payment.authorized\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_h1_auth\",\"order_id\":\"order_h1_other\",\"amount\":250000,\"currency\":\"AED\",\"status\":\"authorized\"}}}}")]
    [InlineData("{\"event\":\"payment.captured\",\"payload\":{}}")]
    public async Task OtherEvents_Ignored(string rawBody)
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_other");

        var response = await PostAsync(rawBody, "evt_h1_other");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ignored", await OutcomeAsync(response));
        Assert.Equal(PaymentStatus.Pending, (await OrderAsync(orderId)).Status);
        Assert.Equal(RetainerInvoiceStatus.Pending, (await InvoiceAsync()).Status);
    }

    [Fact(DisplayName =
        "H1-8: order.paid settles the invoice using its order and payment entities")]
    public async Task OrderPaidEvent_Settles()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_op");

        const string rawBody =
            "{\"event\":\"order.paid\",\"payload\":{" +
            "\"payment\":{\"entity\":{\"id\":\"pay_h1_op\",\"order_id\":\"order_h1_op\"," +
            "\"amount\":250000,\"currency\":\"AED\",\"status\":\"captured\"}}," +
            "\"order\":{\"entity\":{\"id\":\"order_h1_op\",\"status\":\"paid\"}}}}";

        var response = await PostAsync(rawBody, "evt_h1_op");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Paid, (await OrderAsync(orderId)).Status);
        Assert.Equal(RetainerInvoiceStatus.Paid, (await InvoiceAsync()).Status);
    }

    // ==================================================================
    // Idempotency
    // ==================================================================

    [Fact(DisplayName =
        "H1-9: a repeated event id returns 200 and does nothing; the same payment under a new event id is a no-op")]
    public async Task DuplicateEventId_DoesNothing()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_dup");
        var body = PaymentBody("payment.captured", "order_h1_dup", "pay_h1_dup");

        var first = await PostAsync(body, "evt_h1_dup");
        var second = await PostAsync(body, "evt_h1_dup");
        var third = await PostAsync(body, "evt_h1_dup_other");

        Assert.Equal("Completed", await OutcomeAsync(first));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Duplicate", await OutcomeAsync(second));
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal("AlreadyProcessed", await OutcomeAsync(third));

        Assert.Equal(1, await AuditCountAsync(orderId, "PaymentCompletedFromWebhook"));

        await WithScopeDbContextAsync(db =>
        {
            var events = db.WebhookEvents.AsNoTracking()
                .OrderBy(x => x.CreatedAt).ToList();

            Assert.Equal(2, events.Count);
            Assert.Equal(1, events.Count(x => x.EventId == "evt_h1_dup"));
            Assert.Equal("Completed", events.Single(x => x.EventId == "evt_h1_dup").Outcome);
        });
    }

    [Fact(DisplayName =
        "H1-10: concurrent deliveries of one event id are processed exactly once")]
    public async Task ConcurrentDuplicateEventId_ProcessedOnce()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_race");
        var body = PaymentBody("payment.captured", "order_h1_race", "pay_h1_race");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ =>
                Task.Run(() => PostAsync(body, "evt_h1_race"))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var outcomes = new List<string?>();
        foreach (var r in responses)
        {
            outcomes.Add(await OutcomeAsync(r));
        }

        Assert.Single(outcomes, x => x == "Completed");
        Assert.Equal(1, await AuditCountAsync(orderId, "PaymentCompletedFromWebhook"));
    }

    // ==================================================================
    // Signature still enforced
    // ==================================================================

    [Fact(DisplayName =
        "H1-11: a bad signature is still refused (401) and records nothing")]
    public async Task BadSignature_Refused()
    {
        var orderId = await SeedRetainerOrderAsync("order_h1_sig");
        var body = PaymentBody("payment.captured", "order_h1_sig", "pay_h1_sig");

        var response = await PostAsync(body, "evt_h1_sig", signature: "deadbeef");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(PaymentStatus.Pending, (await OrderAsync(orderId)).Status);

        await WithScopeDbContextAsync(db =>
            Assert.Empty(db.WebhookEvents.AsNoTracking().ToList()));
    }
}
