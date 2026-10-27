using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// Hi-1: Payment consistency between manual mark-invoice-paid and the Razorpay
/// webhook, under every ordering and race.
/// <para>
/// Manual mark-paid means the money arrived outside the gateway, so it settles
/// the invoice and CANCELS any open order (review item C2). A capture that
/// arrives after that is never rejected or lost: it is recorded on the order
/// as PaidRequiresReview and the invoice is left alone (review item H1). The
/// invariants are that the invoice is settled exactly once, its PaidAt never
/// moves, and captured money is always recorded on its order.
/// </para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PaymentConsistencyTests : IntegrationTestBase
{
    private const string RetainerMarkPaidUrl =
        "/api/v1/retainer-invoices/{0}/mark-paid";

    private const string WebhookUrl =
        "/api/v1/client/payments/webhook";

    public PaymentConsistencyTests(DatabaseFixture fixture)
        : base(fixture) { }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string Signature(string rawBody)
    {
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(
                FakeRazorpayService.FakeWebhookSecret));
        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();
    }

    private static string BuildBody(
        string orderId,
        string paymentId,
        int amount,
        string currency,
        string status = "captured",
        string evt = "payment.captured")
        => "{\"event\":\"" + evt + "\"," +
           "\"payload\":{\"payment\":{\"entity\":{" +
           "\"id\":\"" + paymentId + "\"," +
           "\"order_id\":\"" + orderId + "\"," +
           "\"amount\":" + amount + "," +
           "\"currency\":\"" + currency + "\"," +
           "\"status\":\"" + status + "\"}}}}";

    private async Task SeedRetainerOrder(
        Guid orderId,
        string razorpayOrderId,
        PaymentStatus status = PaymentStatus.Pending,
        DateTime? paidAt = null,
        DateTime? cancelledAt = null)
    {
        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = orderId,
                InvoiceType = PaymentInvoiceType.RetainerInvoice,
                RetainerInvoiceId = TestDataSeeder.Ids.RetainerInvoiceA,
                ServiceFeeInvoiceId = null,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                Amount = 2500m,
                Currency = "AED",
                Status = status,
                RazorpayOrderId = razorpayOrderId,
                RazorpayPaymentId = null,
                RazorpaySignature = null,
                PaidAt = paidAt,
                FailedAt = null,
                CancelledAt = cancelledAt,
                Notes = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });
    }

    private async Task SeedRetainerInvoice(
        Guid invoiceId,
        RetainerInvoiceStatus status)
    {
        await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == invoiceId);
            inv.Status = status;
            if (status == RetainerInvoiceStatus.Paid)
                inv.PaidAt = DateTime.UtcNow;
            inv.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });
    }

    private async Task<(RetainerInvoiceStatus InvoiceStatus,
        PaymentStatus? OrderStatus,
        int AuditCount)> GetStateAsync()
    {
        RetainerInvoiceStatus invStatus = default;
        PaymentStatus? orderStatus = null;
        int auditCount = 0;

        await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
            invStatus = inv.Status;

            var order = db.PaymentOrders
                .IgnoreQueryFilters()
                .FirstOrDefault(x =>
                    x.RetainerInvoiceId ==
                    TestDataSeeder.Ids.RetainerInvoiceA &&
                    !x.IsDeleted);
            if (order is not null)
                orderStatus = order.Status;

            auditCount = db.AuditLogs.Count(x =>
                x.EntityType == "RetainerInvoice" &&
                x.EntityId == TestDataSeeder.Ids.RetainerInvoiceA);
        });

        return (invStatus, orderStatus, auditCount);
    }

    private async Task<(int ManualAuditCount, int WebhookAuditCount)>
        GetAuditCountsAsync()
    {
        int manualAudit = 0;
        int webhookAudit = 0;

        await WithScopeDbContextAsync(db =>
        {
            manualAudit = db.AuditLogs.Count(x =>
                x.Action == "RetainerInvoicePaid" &&
                x.EntityId ==
                    TestDataSeeder.Ids.RetainerInvoiceA);

            webhookAudit = db.AuditLogs.Count(x =>
                x.Action == "PaymentCompletedFromWebhook");
        });

        return (manualAudit, webhookAudit);
    }

    /// <summary>
    /// D4: reads the invoice/order pair together with both PaidAt values, so a
    /// test can compare a settlement timestamp before and after a call instead
    /// of inferring it from the status.
    /// </summary>
    private async Task<(RetainerInvoiceStatus InvoiceStatus,
        DateTime? InvoicePaidAt,
        PaymentStatus? OrderStatus,
        DateTime? OrderPaidAt)> GetPaidAtStateAsync()
    {
        RetainerInvoiceStatus invStatus = default;
        DateTime? invPaidAt = null;
        PaymentStatus? orderStatus = null;
        DateTime? orderPaidAt = null;

        await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);

            invStatus = inv.Status;
            invPaidAt = inv.PaidAt;

            var order = db.PaymentOrders
                .IgnoreQueryFilters()
                .FirstOrDefault(x =>
                    x.RetainerInvoiceId ==
                    TestDataSeeder.Ids.RetainerInvoiceA &&
                    !x.IsDeleted);

            if (order is not null)
            {
                orderStatus = order.Status;
                orderPaidAt = order.PaidAt;
            }
        });

        return (invStatus, invPaidAt, orderStatus, orderPaidAt);
    }

    private async Task<PaymentOrder> GetOrderAsync()
    {
        PaymentOrder? order = null;

        await WithScopeDbContextAsync(db =>
            order = db.PaymentOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Single(x =>
                    x.RetainerInvoiceId ==
                    TestDataSeeder.Ids.RetainerInvoiceA &&
                    !x.IsDeleted));

        return order!;
    }

    private static async Task<string> OutcomeAsync(
        HttpResponseMessage response)
    {
        var body = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        return (string)body["data"]!["outcome"]!;
    }

    private Task<HttpResponseMessage> PostMarkPaidAsync()
    {
        var url = string.Format(
            RetainerMarkPaidUrl,
            TestDataSeeder.Ids.RetainerInvoiceA);

        return ScopeClient.SendAsync(ScopePatch(
            url,
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    private Task<HttpResponseMessage> PostWebhookAsync(
        string orderId,
        string paymentId = "pay_hi1_0001",
        string? signature = null)
    {
        var rawBody = BuildBody(
            orderId, paymentId, 250000, "AED");

        var request = new HttpRequestMessage(
            HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(
                rawBody, Encoding.UTF8, "application/json")
        };

        if (signature is not null)
            request.Headers.Add(
                "X-Razorpay-Signature", signature);

        request.Headers.Add(
            "X-Razorpay-Event-Id", "evt_hi1_0001");

        return ScopeClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostWebhookDirectAsync(
        string orderId,
        string paymentId = "pay_hi1_0001")
    {
        var rawBody = BuildBody(
            orderId, paymentId, 250000, "AED");

        var request = new HttpRequestMessage(
            HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(
                rawBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            "X-Razorpay-Signature", Signature(rawBody));
        request.Headers.Add(
            "X-Razorpay-Event-Id", "evt_hi1_0001");

        return await ScopeClient.SendAsync(request);
    }

    // ==================================================================
    // Case 1: Manual mark-paid only
    // ==================================================================

    [Fact(DisplayName =
        "Hi-1.1: manual mark-paid settles the invoice and cancels its open order")]
    public async Task ManualPaid_WithOrder_InvoicePaid_OrderCancelled()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0001";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var response = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (invStatus, orderStatus, auditCount) =
            await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.NotNull(orderStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus.Value);
        Assert.Equal(1, auditCount);
    }

    [Fact(DisplayName =
        "Hi-1.2: manual mark-paid without an order sets only invoice to Paid")]
    public async Task ManualPaid_WithoutOrder_InvoicePaid()
    {
        // No PaymentOrder seeded — only invoice is updated.
        var (invStatus, orderStatus, auditCount) =
            await GetStateAsync();

        // Initial state: Pending, no order, no audits.
        Assert.Equal(RetainerInvoiceStatus.Pending, invStatus);
        Assert.Null(orderStatus);

        var response = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (invStatus2, orderStatus2, auditCount2) =
            await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus2);
        Assert.Null(orderStatus2);
        Assert.Equal(1, auditCount2);
    }

    // ==================================================================
    // Case 3: Manual mark-paid followed by webhook (idempotent)
    // ==================================================================

    [Fact(DisplayName =
        "Hi-1.3: a capture after manual mark-paid is recorded for review and leaves the invoice alone")]
    public async Task ManualPaid_ThenWebhook_HeldForReview()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0003";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var manual = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);

        var webhook = await PostWebhookDirectAsync(razorpayId);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal("RequiresReview", await OutcomeAsync(webhook));

        var (invStatus, orderStatus, auditCount) =
            await GetStateAsync();

        // The invoice was already settled and is not touched again; the
        // late money is kept on the order for an admin to refund or allocate.
        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.PaidRequiresReview, orderStatus!.Value);
        Assert.Equal("pay_hi1_0001", (await GetOrderAsync()).RazorpayPaymentId);
        Assert.Equal(1, auditCount);
    }

    // ==================================================================
    // Case 4: Webhook followed by manual (409, no double side effects)
    // ==================================================================

    [Fact(DisplayName =
        "Hi-1.4: manual mark-paid after webhook returns 409 with no double audit")]
    public async Task WebhookPaid_ThenManual_409NoDoubleAudit()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0004";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var webhook = await PostWebhookDirectAsync(razorpayId);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var manual = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.Conflict, manual.StatusCode);

        var (manualAudit, webhookAudit) =
            await GetAuditCountsAsync();

        Assert.Equal(0, manualAudit);
        Assert.Equal(1, webhookAudit);

        var (invStatus, orderStatus, _) = await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
    }

    // ==================================================================
    // Case 5: Concurrent manual + webhook — exactly one transition
    // ==================================================================

    [Fact(DisplayName =
        "Hi-1.5: concurrent manual + webhook settle the invoice once and never lose the payment")]
    public async Task ConcurrentManualAndWebhook_ConsistentState()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0005";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var results = await Task.WhenAll(
            Task.Run(() => PostMarkPaidAsync()),
            Task.Run(() => PostWebhookDirectAsync(razorpayId)));

        // The FOR UPDATE locks serialise the two. Whichever runs second
        // either answers 409 (manual) or records the capture for review
        // (webhook, 200).
        Assert.All(results, r =>
            Assert.True(
                r.StatusCode == HttpStatusCode.OK ||
                r.StatusCode == HttpStatusCode.Conflict,
                $"Unexpected status: {r.StatusCode}"));

        var (invStatus, orderStatus, _) = await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);

        // Webhook first: the order settled the invoice. Manual first: the
        // order was cancelled and the capture is held for review.
        Assert.Contains(
            orderStatus!.Value,
            new[] { PaymentStatus.Paid, PaymentStatus.PaidRequiresReview });

        // Either way the captured money is recorded on the order...
        Assert.Equal("pay_hi1_0001", (await GetOrderAsync()).RazorpayPaymentId);

        // ...and the invoice was settled by exactly one of the two paths.
        var (manualAudit, webhookAudit) =
            await GetAuditCountsAsync();

        Assert.Equal(1, manualAudit + webhookAudit);
        Assert.Equal(
            orderStatus.Value == PaymentStatus.Paid ? 1 : 0, webhookAudit);
    }

    // ==================================================================
    // Case 6: Already-paid — idempotent
    // ==================================================================

    [Fact(DisplayName =
        "Hi-1.6: manual mark-paid on already-paid invoice returns 409")]
    public async Task AlreadyPaid_Manual_409()
    {
        await SeedRetainerInvoice(
            TestDataSeeder.Ids.RetainerInvoiceA,
            RetainerInvoiceStatus.Paid);

        var response = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var (invStatus, orderStatus, auditCount) =
            await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(0, auditCount);
    }

    // ==================================================================
    // Concurrent duplicate manual calls
    // ==================================================================

    [Fact(DisplayName =
        "Hi-1.7: two concurrent manual mark-paid calls produce one success and one 409")]
    public async Task ConcurrentManualDuplicate_OneWins()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0007";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var results = await Task.WhenAll(
            Task.Run(() => PostMarkPaidAsync()),
            Task.Run(() => PostMarkPaidAsync()));

        var okCount = results.Count(
            r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = results.Count(
            r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);

        var (invStatus, orderStatus, _) = await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
    }

    // ==================================================================
    // D4: the PaidAt matrix
    //
    // Every reachable settlement path is now asserted on the timestamps as
    // well as the statuses. The invariant under test is that PaidAt records
    // when a document was FIRST settled: it is written on the transition to
    // Paid, and no later call - repeated manual, repeated webhook, the other
    // path arriving late, or a concurrent race - may move it.
    //
    // A manual settlement cancels the open order rather than paying it, so
    // only the invoice gains a PaidAt; the order's CancelledAt shares the
    // same timestamp because both are written by the same call.
    //
    // Timestamps are compared by capturing the value before the call and
    // comparing after, never by expecting two independent writes to be
    // equal.
    // ==================================================================

    // ------------------------------------------------------------------
    // A. Pending invoice + no order -> manual mark paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-A: manual mark-paid with no order stamps the invoice PaidAt and creates no order")]
    public async Task D4_A_ManualPaid_NoOrder_StampsInvoiceOnly()
    {
        var (_, _, orderBefore, _) =
            await GetPaidAtStateAsync();
        Assert.Null(orderBefore);

        var response = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.NotNull(invPaidAt);
        Assert.Null(orderStatus);
        Assert.Null(orderPaidAt);
    }

    // ------------------------------------------------------------------
    // B. Pending invoice + Created order -> manual mark paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-B: manual mark-paid cancels a Created order; only the invoice gains a PaidAt")]
    public async Task D4_B_ManualPaid_CreatedOrder_CancelsOrder()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0002", PaymentStatus.Created);

        var (_, invPaidAtBefore, _, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Null(invPaidAtBefore);
        Assert.Null(orderPaidAtBefore);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.NotNull(invPaidAt);

        // No gateway payment backs the order, so it never gains a PaidAt;
        // it is retired by the same call that settled the invoice.
        Assert.Null(orderPaidAt);
        Assert.Equal(invPaidAt, (await GetOrderAsync()).CancelledAt);
    }

    // ------------------------------------------------------------------
    // C. Pending invoice + Pending order -> manual mark paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-C: manual mark-paid cancels a Pending order; only the invoice gains a PaidAt")]
    public async Task D4_C_ManualPaid_PendingOrder_CancelsOrder()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0003", PaymentStatus.Pending);

        var (_, invPaidAtBefore, _, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Null(invPaidAtBefore);
        Assert.Null(orderPaidAtBefore);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.NotNull(invPaidAt);

        // No gateway payment backs the order, so it never gains a PaidAt;
        // it is retired by the same call that settled the invoice.
        Assert.Null(orderPaidAt);
        Assert.Equal(invPaidAt, (await GetOrderAsync()).CancelledAt);
    }

    // ------------------------------------------------------------------
    // D. Pending invoice + Cancelled order
    // ------------------------------------------------------------------

    /// <summary>
    /// Review item C2: a cancelled order is history, not a block. Manual
    /// mark-paid settles the invoice and leaves the cancelled order exactly as
    /// it was - it is never resurrected to Paid, because no gateway payment
    /// backs it.
    /// </summary>
    [Fact(DisplayName =
        "D4-D: manual mark-paid with a cancelled order settles the invoice and leaves the order cancelled")]
    public async Task D4_D_ManualPaid_CancelledOrder_SettlesInvoiceOnly()
    {
        var cancelledAt = DateTime.UtcNow.AddHours(-1);

        await SeedRetainerOrder(
            Guid.NewGuid(),
            "order_d4_0004",
            PaymentStatus.Cancelled,
            paidAt: null,
            cancelledAt: cancelledAt);

        var (_, _, orderBefore, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Equal(PaymentStatus.Cancelled, orderBefore!.Value);
        Assert.Null(orderPaidAtBefore);

        var response = await PostMarkPaidAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.NotNull(invPaidAt);

        // The order is untouched: still cancelled, original CancelledAt,
        // and it never gains a PaidAt.
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.Null(orderPaidAt);
        Assert.Equal(
            cancelledAt,
            (await GetOrderAsync()).CancelledAt!.Value.ToUniversalTime(),
            TimeSpan.FromMilliseconds(1));
    }

    // ------------------------------------------------------------------
    // E. Paid invoice + existing order
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-E: manual mark-paid on an already paid invoice is 409 and moves no PaidAt")]
    public async Task D4_E_PaidInvoiceWithOrder_Manual409_PaidAtUnchanged()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0005", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAtAfterFirst, _, orderPaidAtAfterFirst) =
            await GetPaidAtStateAsync();
        Assert.NotNull(invPaidAtAfterFirst);
        Assert.Null(orderPaidAtAfterFirst);

        var second = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterFirst, invPaidAt);
        Assert.Null(orderPaidAt);
    }

    // ------------------------------------------------------------------
    // F. Webhook first
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-F: a captured webhook settles the pair with one equal PaidAt")]
    public async Task D4_F_WebhookFirst_SettlesBothWithOnePaidAt()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0006", PaymentStatus.Pending);

        var (_, _, _, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Null(orderPaidAtBefore);

        var response =
            await PostWebhookDirectAsync("order_d4_0006");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.NotNull(invPaidAt);
        Assert.Equal(invPaidAt, orderPaidAt);
    }

    // ------------------------------------------------------------------
    // G. Manual first
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-G: manual first settles the invoice and retires the order with the same timestamp")]
    public async Task D4_G_ManualFirst_SettlesInvoice_RetiresOrder()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0007", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.NotNull(invPaidAt);
        Assert.Null(orderPaidAt);
        Assert.Equal(invPaidAt, (await GetOrderAsync()).CancelledAt);
    }

    // ------------------------------------------------------------------
    // H. Repeated webhook
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-H: a repeated captured webhook is a no-op that moves no PaidAt")]
    public async Task D4_H_RepeatedWebhook_NoOp_PaidAtUnchanged()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0008", PaymentStatus.Pending);

        var first =
            await PostWebhookDirectAsync("order_d4_0008");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var (_, invPaidAtAfterFirst, _, orderPaidAtAfterFirst) =
            await GetPaidAtStateAsync();
        Assert.NotNull(invPaidAtAfterFirst);
        Assert.NotNull(orderPaidAtAfterFirst);

        var second =
            await PostWebhookDirectAsync("order_d4_0008");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var body = System.Text.Json.Nodes.JsonNode.Parse(
            await second.Content.ReadAsStringAsync())!;
        Assert.False((bool)body["data"]!["completed"]!);

        var (_, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterFirst, invPaidAt);
        Assert.Equal(orderPaidAtAfterFirst, orderPaidAt);
    }

    // ------------------------------------------------------------------
    // I. Repeated manual mark-paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-I: a repeated manual mark-paid is 409 and moves no PaidAt")]
    public async Task D4_I_RepeatedManual_409_PaidAtUnchanged()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0009", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAtAfterFirst, _, orderPaidAtAfterFirst) =
            await GetPaidAtStateAsync();
        Assert.NotNull(invPaidAtAfterFirst);
        Assert.Null(orderPaidAtAfterFirst);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterFirst, invPaidAt);
        Assert.Null(orderPaidAt);

        // A refused call must not audit either.
        var (manualAudit, _) = await GetAuditCountsAsync();
        Assert.Equal(1, manualAudit);
    }

    // ------------------------------------------------------------------
    // J. Webhook after manual mark-paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-J: a captured webhook after manual mark-paid is held for review and moves no invoice PaidAt")]
    public async Task D4_J_WebhookAfterManual_HeldForReview_InvoicePaidAtUnchanged()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0010", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAtAfterManual, _, _) =
            await GetPaidAtStateAsync();
        Assert.NotNull(invPaidAtAfterManual);

        var webhook =
            await PostWebhookDirectAsync("order_d4_0010");
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal("RequiresReview", await OutcomeAsync(webhook));

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        // The invoice keeps its first settlement; the order records when
        // the late money was captured.
        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(invPaidAtAfterManual, invPaidAt);
        Assert.Equal(PaymentStatus.PaidRequiresReview, orderStatus!.Value);
        Assert.NotNull(orderPaidAt);
    }

    // ------------------------------------------------------------------
    // K. Manual mark-paid after webhook
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-K: manual mark-paid after the webhook is 409 and moves no PaidAt")]
    public async Task D4_K_ManualAfterWebhook_409_PaidAtUnchanged()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0011", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostWebhookDirectAsync("order_d4_0011"))
                .StatusCode);

        var (_, invPaidAtAfterWebhook, _, orderPaidAtAfterWebhook) =
            await GetPaidAtStateAsync();
        Assert.NotNull(invPaidAtAfterWebhook);
        Assert.NotNull(orderPaidAtAfterWebhook);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterWebhook, invPaidAt);
        Assert.Equal(orderPaidAtAfterWebhook, orderPaidAt);
    }

    // ------------------------------------------------------------------
    // L. Concurrent webhook + manual mark-paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-L: concurrent webhook and manual mark-paid settle the invoice once with one stable PaidAt")]
    public async Task D4_L_Concurrent_LeavesOnePaidAt()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0012", PaymentStatus.Pending);

        var (_, _, _, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Null(orderPaidAtBefore);

        var results = await Task.WhenAll(
            Task.Run(() => PostMarkPaidAsync()),
            Task.Run(() => PostWebhookDirectAsync("order_d4_0012")));

        Assert.All(results, r => Assert.True(
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status: {(int)r.StatusCode}"));

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.NotNull(invPaidAt);

        // The capture is recorded either way.
        Assert.NotNull(orderPaidAt);

        if (orderStatus!.Value == PaymentStatus.Paid)
        {
            // Webhook first: one call settled both with one timestamp.
            Assert.Equal(invPaidAt, orderPaidAt);
        }
        else
        {
            // Manual first: the late capture is held for review.
            Assert.Equal(PaymentStatus.PaidRequiresReview, orderStatus.Value);
        }

        // Replaying both paths changes nothing.
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostMarkPaidAsync()).StatusCode);
        await PostWebhookDirectAsync("order_d4_0012");

        var (_, invPaidAtAfter, orderStatusAfter, orderPaidAtAfter) =
            await GetPaidAtStateAsync();
        Assert.Equal(invPaidAt, invPaidAtAfter);
        Assert.Equal(orderStatus, orderStatusAfter);
        Assert.Equal(orderPaidAt, orderPaidAtAfter);
    }

    // ------------------------------------------------------------------
    // M. Split state: order Paid, invoice Pending
    // ------------------------------------------------------------------

    /// <summary>
    /// Seeded directly: neither handler can produce it, because each writes
    /// the order and the invoice in a single transaction. It is the only state
    /// where the unconditional PaidAt assignment was observable, so D4 pins it
    /// explicitly - the invoice settles and gains a first PaidAt while the
    /// order keeps the capture timestamp it already had.
    /// </summary>
    [Fact(DisplayName =
        "D4-M: a split order-paid/invoice-pending state settles the invoice and preserves the order's original PaidAt")]
    public async Task D4_M_SplitState_PreservesOriginalOrderPaidAt()
    {
        var originalOrderPaidAt =
            new DateTime(2024, 6, 11, 14, 5, 0, DateTimeKind.Utc);

        await SeedRetainerOrder(
            Guid.NewGuid(),
            "order_d4_0013",
            PaymentStatus.Paid,
            paidAt: originalOrderPaidAt);

        // The split state the handler will observe.
        var (invBefore, invPaidAtBefore, orderBefore, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Equal(RetainerInvoiceStatus.Pending, invBefore);
        Assert.Null(invPaidAtBefore);
        Assert.Equal(PaymentStatus.Paid, orderBefore!.Value);
        Assert.NotNull(orderPaidAtBefore);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        // The invoice is settled for the first time.
        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.NotNull(invPaidAt);

        // The order was already settled, so its PaidAt is untouched.
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.Equal(
            originalOrderPaidAt,
            orderPaidAt!.Value.ToUniversalTime());
    }
}
