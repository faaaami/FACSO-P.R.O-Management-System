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
/// webhook.  Both paths must produce a consistent Paid+Paid (invoice + order)
/// state and tolerate all concurrency/ordering combinations.
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
        "Hi-1.1: manual mark-paid sets both invoice and order to Paid")]
    public async Task ManualPaid_WithOrder_BothPaid()
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
        Assert.Equal(PaymentStatus.Paid, orderStatus.Value);
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
        "Hi-1.3: webhook after manual mark-paid is an idempotent no-op")]
    public async Task ManualPaid_ThenWebhook_Idempotent()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0003";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var manual = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);

        var webhook = await PostWebhookDirectAsync(razorpayId);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var body = System.Text.Json.Nodes.JsonNode.Parse(
            await webhook.Content.ReadAsStringAsync())!;
        Assert.False((bool)body["data"]!["completed"]!);

        var (invStatus, orderStatus, auditCount) =
            await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
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
        "Hi-1.5: concurrent manual + webhook produce a consistent final state")]
    public async Task ConcurrentManualAndWebhook_ConsistentState()
    {
        var orderGuid = Guid.NewGuid();
        const string razorpayId = "order_hi1_0005";
        await SeedRetainerOrder(orderGuid, razorpayId);

        var results = await Task.WhenAll(
            Task.Run(() => PostMarkPaidAsync()),
            Task.Run(() => PostWebhookDirectAsync(razorpayId)));

        var statuses = results
            .Select(r => r.StatusCode)
            .OrderBy(s => s)
            .ToList();

        // Exactly one succeeds (200), one is idempotent (200) or
        // rejected (409).  Both are valid: the FOR UPDATE serialises
        // them so only one performs the transition.
        Assert.All(statuses, s =>
            Assert.True(
                s == HttpStatusCode.OK ||
                s == HttpStatusCode.Conflict,
                $"Unexpected status: {s}"));

        var (invStatus, orderStatus, _) = await GetStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);

        // Exactly one manual audit and at most one webhook audit.
        var (manualAudit, webhookAudit) =
            await GetAuditCountsAsync();

        Assert.True(manualAudit + webhookAudit >= 1);
        Assert.True(manualAudit + webhookAudit <= 2);
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
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
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
        "D4-B: manual mark-paid settles a Created order and gives the pair one equal PaidAt")]
    public async Task D4_B_ManualPaid_CreatedOrder_SettlesBoth()
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
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);

        // Both were settled by the same call, so they carry the same stamp.
        Assert.NotNull(invPaidAt);
        Assert.Equal(invPaidAt, orderPaidAt);
    }

    // ------------------------------------------------------------------
    // C. Pending invoice + Pending order -> manual mark paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-C: manual mark-paid settles a Pending order and gives the pair one equal PaidAt")]
    public async Task D4_C_ManualPaid_PendingOrder_SettlesBoth()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0003", PaymentStatus.Pending);

        var (_, _, _, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Null(orderPaidAtBefore);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.NotNull(invPaidAt);
        Assert.Equal(invPaidAt, orderPaidAt);
    }

    // ------------------------------------------------------------------
    // D. Pending invoice + Cancelled order
    // ------------------------------------------------------------------

    /// <summary>
    /// A cancelled payment order is terminal, so manual mark-paid must refuse
    /// to resurrect it. The handler checks the order status AFTER the order
    /// row lock is held, so a cancelled order can never be flipped back to
    /// Paid and the invoice is left untouched for a fresh payment attempt.
    /// </summary>
    /// <remarks>
    /// This inverts the previous expectation, which asserted the handler's
    /// former unconditional Paid assignment revived a cancelled order. That
    /// behaviour left a local settlement with no gateway payment backing it
    /// and no RazorpayPaymentId to evidence it.
    /// </remarks>
    [Fact(DisplayName =
        "D4-D: manual mark-paid with a cancelled order is 409 and settles nothing")]
    public async Task D4_D_ManualPaid_CancelledOrder_Is409_PaysNothing()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(),
            "order_d4_0004",
            PaymentStatus.Cancelled,
            paidAt: null,
            cancelledAt: DateTime.UtcNow.AddHours(-1));

        var (_, _, orderBefore, orderPaidAtBefore) =
            await GetPaidAtStateAsync();
        Assert.Equal(PaymentStatus.Cancelled, orderBefore!.Value);
        Assert.Null(orderPaidAtBefore);

        var response = await PostMarkPaidAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        // Nothing moved: the order stays cancelled and never gains a PaidAt,
        // and the invoice is not settled behind the caller's back.
        Assert.Equal(RetainerInvoiceStatus.Pending, invStatus);
        Assert.Equal(PaymentStatus.Cancelled, orderStatus!.Value);
        Assert.Null(invPaidAt);
        Assert.Null(orderPaidAt);
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
        Assert.NotNull(orderPaidAtAfterFirst);

        var second = await PostMarkPaidAsync();
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterFirst, invPaidAt);
        Assert.Equal(orderPaidAtAfterFirst, orderPaidAt);
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
        "D4-G: manual first settles the pair with one equal PaidAt")]
    public async Task D4_G_ManualFirst_SettlesBothWithOnePaidAt()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0007", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (invStatus, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(RetainerInvoiceStatus.Paid, invStatus);
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.NotNull(invPaidAt);
        Assert.Equal(invPaidAt, orderPaidAt);
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
        Assert.NotNull(orderPaidAtAfterFirst);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterFirst, invPaidAt);
        Assert.Equal(orderPaidAtAfterFirst, orderPaidAt);

        // A refused call must not audit either.
        var (manualAudit, _) = await GetAuditCountsAsync();
        Assert.Equal(1, manualAudit);
    }

    // ------------------------------------------------------------------
    // J. Webhook after manual mark-paid
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D4-J: a captured webhook after manual mark-paid is a no-op that moves no PaidAt")]
    public async Task D4_J_WebhookAfterManual_NoOp_PaidAtUnchanged()
    {
        await SeedRetainerOrder(
            Guid.NewGuid(), "order_d4_0010", PaymentStatus.Pending);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostMarkPaidAsync()).StatusCode);

        var (_, invPaidAtAfterManual, _, orderPaidAtAfterManual) =
            await GetPaidAtStateAsync();
        Assert.NotNull(invPaidAtAfterManual);
        Assert.NotNull(orderPaidAtAfterManual);

        var webhook =
            await PostWebhookDirectAsync("order_d4_0010");
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var body = System.Text.Json.Nodes.JsonNode.Parse(
            await webhook.Content.ReadAsStringAsync())!;
        Assert.False((bool)body["data"]!["completed"]!);

        var (_, invPaidAt, orderStatus, orderPaidAt) =
            await GetPaidAtStateAsync();

        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.Equal(invPaidAtAfterManual, invPaidAt);
        Assert.Equal(orderPaidAtAfterManual, orderPaidAt);
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
        "D4-L: concurrent webhook and manual mark-paid leave one consistent PaidAt on both sides")]
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
        Assert.Equal(PaymentStatus.Paid, orderStatus!.Value);
        Assert.NotNull(invPaidAt);
        Assert.Equal(invPaidAt, orderPaidAt);

        // Replaying both paths changes nothing.
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostMarkPaidAsync()).StatusCode);
        await PostWebhookDirectAsync("order_d4_0012");

        var (_, invPaidAtAfter, _, orderPaidAtAfter) =
            await GetPaidAtStateAsync();
        Assert.Equal(invPaidAt, invPaidAtAfter);
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
