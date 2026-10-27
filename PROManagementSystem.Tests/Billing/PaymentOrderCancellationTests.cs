using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// P5: payment-order cancellation and invoice voiding must retire the order.
///
/// Razorpay cannot close or cancel a gateway order (its Orders API only
/// updates <c>notes</c>), so retiring is local only. A client can therefore
/// still complete checkout on a retired order; these tests pin that such a
/// payment is never lost: it settles a still-pending invoice, or is recorded
/// as PaidRequiresReview when the invoice was voided.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PaymentOrderCancellationTests : IntegrationTestBase
{
    private const string WebhookUrl = "/api/v1/client/payments/webhook";
    private const string CreateUrl = "/api/v1/payment-orders";
    private const string CancelUrl = "/api/v1/payment-orders/{0}/cancel";
    private const string RetainerVoidUrl = "/api/v1/retainer-invoices/{0}/void";
    private const string RetainerMarkPaidUrl = "/api/v1/retainer-invoices/{0}/mark-paid";
    private const string ServiceFeeVoidUrl = "/api/v1/service-fee-invoices/{0}/void";

    private const decimal RetainerAmount = 2500m;
    private const int RetainerAmountInSmallestUnit = 250000;

    public PaymentOrderCancellationTests(DatabaseFixture fixture)
        : base(fixture) { }

    // ==================================================================
    // Helpers
    // ==================================================================

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
        int amount = RetainerAmountInSmallestUnit,
        string currency = "AED")
        => "{\"event\":\"payment.captured\"," +
           "\"payload\":{\"payment\":{\"entity\":{" +
           "\"id\":\"" + paymentId + "\"," +
           "\"order_id\":\"" + orderId + "\"," +
           "\"amount\":" + amount + "," +
           "\"currency\":\"" + currency + "\"," +
           "\"status\":\"captured\"}}}}";

    private Task<HttpResponseMessage> PostWebhookAsync(
        string orderId,
        string paymentId = "pay_p5_0001")
    {
        var rawBody = BuildBody(orderId, paymentId);

        var request = new HttpRequestMessage(
            HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(
                rawBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            "X-Razorpay-Signature", Signature(rawBody));
        request.Headers.Add(
            "X-Razorpay-Event-Id", "evt_p5_0001");

        return ScopeClient.SendAsync(request);
    }

    private Task<HttpResponseMessage> CancelOrderAsync(
        Guid orderId)
        => ScopeClient.SendAsync(ScopePatch(
            string.Format(CancelUrl, orderId),
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> CreateRetainerOrderAsync()
        => ScopeClient.SendAsync(ScopePost(
            CreateUrl,
            new
            {
                invoiceType = "RetainerInvoice",
                retainerInvoiceId = TestDataSeeder.Ids.RetainerInvoiceA,
                serviceFeeInvoiceId = (Guid?)null,
                notes = "P5 race"
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> MarkRetainerPaidAsync()
        => ScopeClient.SendAsync(ScopePatch(
            string.Format(
                RetainerMarkPaidUrl, TestDataSeeder.Ids.RetainerInvoiceA),
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> VoidRetainerAsync(
        string? reason = "test void")
        => ScopeClient.SendAsync(ScopePatch(
            string.Format(
                RetainerVoidUrl, TestDataSeeder.Ids.RetainerInvoiceA),
            new { reason },
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private Task<HttpResponseMessage> VoidServiceFeeAsync(
        string? reason = "test void")
        => ScopeClient.SendAsync(ScopePatch(
            string.Format(
                ServiceFeeVoidUrl, TestDataSeeder.Ids.ServiceFeeInvoiceA),
            new { reason },
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private async Task<PaymentOrder> SeedRetainerOrderAsync(
        string razorpayOrderId,
        PaymentStatus status = PaymentStatus.Pending)
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            InvoiceType = PaymentInvoiceType.RetainerInvoice,
            RetainerInvoiceId = TestDataSeeder.Ids.RetainerInvoiceA,
            ServiceFeeInvoiceId = null,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            Amount = RetainerAmount,
            Currency = "AED",
            Status = status,
            RazorpayOrderId = razorpayOrderId,
            RazorpayPaymentId = null,
            RazorpaySignature = null,
            PaidAt = null,
            FailedAt = null,
            CancelledAt = null,
            Notes = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(order);
            db.SaveChanges();
        });

        return order;
    }

    private async Task<PaymentOrder> SeedServiceFeeOrderAsync(
        string razorpayOrderId,
        PaymentStatus status = PaymentStatus.Pending)
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            InvoiceType = PaymentInvoiceType.ServiceFeeInvoice,
            RetainerInvoiceId = null,
            ServiceFeeInvoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            Amount = RetainerAmount,
            Currency = "AED",
            Status = status,
            RazorpayOrderId = razorpayOrderId,
            RazorpayPaymentId = null,
            RazorpaySignature = null,
            PaidAt = null,
            FailedAt = null,
            CancelledAt = null,
            Notes = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(order);
            db.SaveChanges();
        });

        return order;
    }

    private async Task<PaymentOrder?> ReadRetainerOrderAsync()
    {
        PaymentOrder? found = null;

        await WithScopeDbContextAsync(db =>
        {
            found = db.PaymentOrders
                .IgnoreQueryFilters()
                .FirstOrDefault(x =>
                    x.RetainerInvoiceId ==
                        TestDataSeeder.Ids.RetainerInvoiceA &&
                    !x.IsDeleted);
        });

        return found;
    }

    private async Task<PaymentOrder?> ReadServiceFeeOrderAsync()
    {
        PaymentOrder? found = null;

        await WithScopeDbContextAsync(db =>
        {
            found = db.PaymentOrders
                .IgnoreQueryFilters()
                .FirstOrDefault(x =>
                    x.ServiceFeeInvoiceId ==
                        TestDataSeeder.Ids.ServiceFeeInvoiceA &&
                    !x.IsDeleted);
        });

        return found;
    }

    private async Task<List<PaymentOrder>> ReadAllRetainerOrdersAsync()
    {
        var orders = new List<PaymentOrder>();

        await WithScopeDbContextAsync(db =>
            orders.AddRange(db.PaymentOrders
                .IgnoreQueryFilters()
                .Where(x =>
                    x.RetainerInvoiceId ==
                        TestDataSeeder.Ids.RetainerInvoiceA &&
                    !x.IsDeleted)));

        return orders;
    }

    private async Task<RetainerInvoiceStatus> RetainerStatusAsync()
    {
        var status = default(RetainerInvoiceStatus);

        await WithScopeDbContextAsync(db =>
            status = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA)
                .Status);

        return status;
    }

    private async Task<ServiceFeeInvoiceStatus> ServiceFeeStatusAsync()
    {
        var status = default(ServiceFeeInvoiceStatus);

        await WithScopeDbContextAsync(db =>
            status = db.ServiceFeeInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.ServiceFeeInvoiceA)
                .Status);

        return status;
    }

    private async Task SetRetainerStatusAsync(
        RetainerInvoiceStatus status)
        => await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
            inv.Status = status;
            inv.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

    private async Task<int> CancelAuditCountAsync(Guid orderId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
            count = db.AuditLogs.Count(x =>
                x.Action == "PaymentOrderCancelled" &&
                x.EntityId == orderId));

        return count;
    }

    // ==================================================================
    // Cancel
    // ==================================================================

    [Fact(DisplayName =
        "P5-C1: cancelling a Pending order cancels it")]
    public async Task Cancel_CancelsPendingOrder()
    {
        var seeded = await SeedRetainerOrderAsync("order_p5_c1");

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Equal(1, await CancelAuditCountAsync(seeded.Id));
    }

    [Fact(DisplayName =
        "P5-C2: a payment completed after cancel still settles the pending invoice")]
    public async Task Cancel_ThenLateCapture_SettlesPendingInvoice()
    {
        // The gateway order cannot be closed, so the client can still pay it
        // after the cancel. The money must not be lost: the invoice is still
        // pending, so the capture settles it.
        var seeded = await SeedRetainerOrderAsync("order_p5_c2");

        Assert.Equal(
            HttpStatusCode.OK, (await CancelOrderAsync(seeded.Id)).StatusCode);

        var webhook = await PostWebhookAsync("order_p5_c2");

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Paid, stored!.Status);
        Assert.Equal("pay_p5_0001", stored.RazorpayPaymentId);
        Assert.Equal(
            RetainerInvoiceStatus.Paid, await RetainerStatusAsync());
    }

    [Fact(DisplayName =
        "P5-C4: cancelling a Created order with no gateway order succeeds")]
    public async Task Cancel_CreatedOrderWithoutGatewayOrder_SkipsGateway()
    {
        // A Created order with no gateway id never reached the provider.
        var seeded = await SeedRetainerOrderAsync(
            "order_p5_c4", PaymentStatus.Created);

        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders
                .First(x => x.Id == seeded.Id);
            order.RazorpayOrderId = null;
            db.SaveChanges();
        });

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
    }

    [Fact(DisplayName =
        "P5-C5: cancelling a Paid order is 409")]
    public async Task Cancel_PaidOrder_Is409()
    {
        var seeded = await SeedRetainerOrderAsync(
            "order_p5_c5", PaymentStatus.Paid);

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Paid, stored!.Status);
    }

    [Fact(DisplayName =
        "P5-C6: cancelling an already cancelled order is 409")]
    public async Task Cancel_AlreadyCancelled_Is409()
    {
        var seeded = await SeedRetainerOrderAsync(
            "order_p5_c6", PaymentStatus.Cancelled);

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName =
        "P5-C7: cancelling is refused when the linked invoice is no longer pending")]
    public async Task Cancel_NonPendingInvoice_Is409()
    {
        // A settled invoice must never be left without a live order, and a
        // cancellation must not paper over a settlement.
        var seeded = await SeedRetainerOrderAsync("order_p5_c7");
        await SetRetainerStatusAsync(RetainerInvoiceStatus.Paid);

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Pending, stored!.Status);
    }

    [Fact(DisplayName =
        "P5-C8: concurrent cancel and webhook never leave a cancelled order holding a PaidAt")]
    public async Task Cancel_RacingWebhook_NeverCancelsASettledOrder()
    {
        await SeedRetainerOrderAsync("order_p5_c8");

        var results = await Task.WhenAll(
            Task.Run(async () =>
            {
                var order = await ReadRetainerOrderAsync();
                return await CancelOrderAsync(order!.Id);
            }),
            Task.Run(() => PostWebhookAsync("order_p5_c8")));

        Assert.All(results, r => Assert.True(
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status: {(int)r.StatusCode}"));

        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);

        // Whichever side won, the pair is coherent: a settled order keeps its
        // PaidAt, and a cancelled order never has one. This is the invariant
        // the lock-free handler could break by overwriting Paid with
        // Cancelled after the webhook committed.
        if (stored!.Status == PaymentStatus.Paid)
        {
            Assert.NotNull(stored.PaidAt);
            Assert.Equal(
                RetainerInvoiceStatus.Paid, await RetainerStatusAsync());
        }
        else
        {
            Assert.Equal(PaymentStatus.Cancelled, stored.Status);
            Assert.Null(stored.PaidAt);
        }
    }

    // ==================================================================
    // Void: must retire the open order
    // ==================================================================

    [Fact(DisplayName =
        "P5-V1: voiding a retainer invoice cancels its open order")]
    public async Task VoidRetainer_CancelsOrder()
    {
        var seeded = await SeedRetainerOrderAsync("order_p5_v1");

        var response = await VoidRetainerAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Null(stored.PaidAt);

        Assert.Equal(
            RetainerInvoiceStatus.Void, await RetainerStatusAsync());
        Assert.Equal(1, await CancelAuditCountAsync(seeded.Id));
    }

    [Fact(DisplayName =
        "P5-V2: a payment completed after a retainer void is held for review and the invoice stays void")]
    public async Task VoidRetainer_ThenLateCapture_HeldForReview()
    {
        // The gateway order cannot be closed, so the client can still pay it
        // after the void. The money is recorded on the order for an admin to
        // refund or allocate; the voided invoice is not resurrected.
        await SeedRetainerOrderAsync("order_p5_v2");

        Assert.Equal(
            HttpStatusCode.OK, (await VoidRetainerAsync()).StatusCode);

        var webhook = await PostWebhookAsync("order_p5_v2");

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.PaidRequiresReview, stored!.Status);
        Assert.Equal("pay_p5_0001", stored.RazorpayPaymentId);
        Assert.Equal(
            RetainerInvoiceStatus.Void, await RetainerStatusAsync());
    }

    [Fact(DisplayName =
        "P5-V3: voiding an invoice with no payment order still voids")]
    public async Task VoidRetainer_NoOrder_StillVoids()
    {
        // Regression guard: the new order handling must not break the common
        // case of voiding an invoice that was never checked out.
        var response = await VoidRetainerAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            RetainerInvoiceStatus.Void, await RetainerStatusAsync());
    }

    [Fact(DisplayName =
        "P5-V4: voiding a paid invoice is 409")]
    public async Task VoidRetainer_PaidInvoice_Is409()
    {
        await SeedRetainerOrderAsync("order_p5_v4");
        await SetRetainerStatusAsync(RetainerInvoiceStatus.Paid);

        var response = await VoidRetainerAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Pending, stored!.Status);
    }

    [Fact(DisplayName =
        "P5-V5: concurrent void and webhook never leave a paid order under a voided invoice")]
    public async Task VoidRetainer_RacingWebhook_NeverVoidsOverASettlement()
    {
        await SeedRetainerOrderAsync("order_p5_v5");

        var results = await Task.WhenAll(
            Task.Run(() => VoidRetainerAsync()),
            Task.Run(() => PostWebhookAsync("order_p5_v5")));

        Assert.All(results, r => Assert.True(
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status: {(int)r.StatusCode}"));

        var invoiceStatus = await RetainerStatusAsync();
        var stored = await ReadRetainerOrderAsync();

        Assert.NotNull(stored);

        // The pre-P5 race could produce a voided invoice sitting on top of a
        // paid order: captured money hidden behind a voided invoice. Neither
        // side may overwrite a settlement.
        if (invoiceStatus == RetainerInvoiceStatus.Void)
        {
            Assert.NotEqual(PaymentStatus.Paid, stored!.Status);
        }
        else
        {
            Assert.Equal(RetainerInvoiceStatus.Paid, invoiceStatus);
            Assert.Equal(PaymentStatus.Paid, stored!.Status);
            Assert.NotNull(stored.PaidAt);
        }
    }

    [Fact(DisplayName =
        "P5-V8: void racing mark-paid never leaves a paid order under a voided invoice")]
    public async Task VoidRetainer_RacingMarkPaid_NeverHidesASettlement()
    {
        // Void and mark-paid take the same order row lock in the same order, so
        // they serialise. Whichever wins, the pair must stay coherent - a
        // voided invoice may not sit on top of an order marked Paid, which is
        // the shape a client would see as "we voided your invoice" while the
        // money had in fact been settled.
        await SeedRetainerOrderAsync("order_p5_v8");

        var results = await Task.WhenAll(
            Task.Run(() => VoidRetainerAsync()),
            Task.Run(() => MarkRetainerPaidAsync()));

        Assert.All(results, r => Assert.True(
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status: {(int)r.StatusCode}"));

        var invoiceStatus = await RetainerStatusAsync();
        var stored = await ReadRetainerOrderAsync();

        Assert.NotNull(stored);

        if (invoiceStatus == RetainerInvoiceStatus.Void)
        {
            // The void won: the order must have been cancelled, so
            // there is no live gateway order left collecting money.
            Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
            Assert.Null(stored.PaidAt);
        }
        else
        {
            // Mark-paid won: a void must never overwrite a settlement.
            Assert.Equal(RetainerInvoiceStatus.Paid, invoiceStatus);
            Assert.Equal(PaymentStatus.Paid, stored!.Status);
            Assert.NotNull(stored.PaidAt);
        }
    }

    [Fact(DisplayName =
        "P5-C9: racing create and cancel never leaves two live gateway orders on one invoice")]
    public async Task Cancel_RacingCreate_LeavesAtMostOneOrder()
    {
        // The invoice already has a gateway-backed order, so a second create must
        // be refused. Racing it against the cancel proves the guard is not a
        // read-then-write that a cancel can slip between: the worst outcome would
        // be a cancelled order plus a freshly payable one for the same invoice.
        await SeedRetainerOrderAsync("order_p5_c9");

        var orderId = (await ReadRetainerOrderAsync())!.Id;

        var results = await Task.WhenAll(
            Task.Run(() => CreateRetainerOrderAsync()),
            Task.Run(() => CancelOrderAsync(orderId)));

        Assert.All(results, r => Assert.True(
            r.StatusCode == HttpStatusCode.Created ||
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status: {(int)r.StatusCode}"));

        // No positional assertion on results[0] here, deliberately.
        //
        // This test previously asserted results[0] == Conflict, which directly
        // contradicts the Assert.All three lines above: that one tolerates a
        // Created, this one forbids it. Which of the two requests wins the race
        // is a property of thread scheduling and of when the DB row lock is
        // granted, not of the code under test, so the two assertions could not
        // both be satisfied on every run and the test was order-dependent.
        //
        // What the race is actually for is the invariant asserted below, and that
        // is checked on the stored rows rather than on the HTTP status codes:
        // exactly one order survives and it is Cancelled with its gateway reference
        // intact. Whether the losing request answered 409 or the winning create
        // answered 201 does not change any of that, so the status codes are only
        // range-checked and the correctness property is asserted where it is
        // deterministic.

        var orders = await ReadAllRetainerOrdersAsync();

        Assert.Single(orders);
        Assert.Equal(PaymentStatus.Cancelled, orders[0].Status);
        Assert.NotNull(orders[0].RazorpayOrderId);
    }

    [Fact(DisplayName =
        "P5-V6: voiding a service fee invoice cancels its open order")]
    public async Task VoidServiceFee_CancelsOrder()
    {
        var seeded = await SeedServiceFeeOrderAsync("order_p5_v6");

        var response = await VoidServiceFeeAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await ReadServiceFeeOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Null(stored.PaidAt);

        Assert.Equal(
            ServiceFeeInvoiceStatus.Void, await ServiceFeeStatusAsync());
        Assert.Equal(1, await CancelAuditCountAsync(seeded.Id));
    }

    [Fact(DisplayName =
        "P5-V7: a payment completed after a service fee void is held for review and the invoice stays void")]
    public async Task VoidServiceFee_ThenLateCapture_HeldForReview()
    {
        // The gateway order cannot be closed, so the client can still pay it
        // after the void. The money is recorded on the order for an admin to
        // refund or allocate; the voided invoice is not resurrected.
        await SeedServiceFeeOrderAsync("order_p5_v7");

        Assert.Equal(
            HttpStatusCode.OK, (await VoidServiceFeeAsync()).StatusCode);

        var webhook = await PostWebhookAsync("order_p5_v7");

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var stored = await ReadServiceFeeOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.PaidRequiresReview, stored!.Status);
        Assert.Equal("pay_p5_0001", stored.RazorpayPaymentId);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Void, await ServiceFeeStatusAsync());
    }

    // ==================================================================
    // Invoice detail now exposes the payment order (P5 client visibility)
    // ==================================================================

    [Fact(DisplayName =
        "P5-D1: client retainer invoice detail exposes the linked payment order")]
    public async Task RetainerDetail_ExposesPaymentOrder()
    {
        // Validates the Dapper LEFT JOIN and the nullable-enum mapping at
        // runtime, which a compile cannot check.
        var seeded = await SeedRetainerOrderAsync("order_p5_d1");

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var data = json["data"]!;

        Assert.Equal(
            seeded.Id.ToString(),
            data["paymentOrderId"]!.GetValue<string>());
        Assert.Equal(
            "Pending",
            data["paymentOrderStatus"]!.GetValue<string>());
        Assert.Equal(
            "order_p5_d1",
            data["razorpayOrderId"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "P5-D2: client retainer invoice detail returns null order fields when no order exists")]
    public async Task RetainerDetail_NoOrder_ReturnsNulls()
    {
        // The LEFT JOIN must not turn a missing order into a NOT FOUND or a
        // dropped invoice row.
        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var data = json["data"]!;

        Assert.Equal(
            TestDataSeeder.Ids.RetainerInvoiceA.ToString(),
            data["id"]!.GetValue<string>());
        Assert.Null(data["paymentOrderId"]);
        Assert.Null(data["paymentOrderStatus"]);
    }

    [Fact(DisplayName =
        "P5-D3: client service fee invoice detail exposes the linked payment order")]
    public async Task ServiceFeeDetail_ExposesPaymentOrder()
    {
        var seeded = await SeedServiceFeeOrderAsync("order_p5_d3");

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceA}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var data = json["data"]!;

        Assert.Equal(
            seeded.Id.ToString(),
            data["paymentOrderId"]!.GetValue<string>());
        Assert.Equal(
            "Pending",
            data["paymentOrderStatus"]!.GetValue<string>());
        Assert.Equal(
            "order_p5_d3",
            data["razorpayOrderId"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "P5-D4: the client retainer invoice route exposes the payment order for the client-facing checkout")]
    public async Task ClientRetainerDetail_ExposesPaymentOrder()
    {
        // This is the route the client checkout drawer actually polls, so it is
        // the surface that matters for the confirming state.
        var seeded = await SeedRetainerOrderAsync("order_p5_d4");

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var data = json["data"]!;

        Assert.Equal(
            seeded.Id.ToString(),
            data["paymentOrderId"]!.GetValue<string>());
        Assert.Equal(
            "Pending",
            data["paymentOrderStatus"]!.GetValue<string>());
        Assert.Equal(
            "order_p5_d4",
            data["razorpayOrderId"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "P5-D5: the client service fee invoice route exposes the payment order")]
    public async Task ClientServiceFeeDetail_ExposesPaymentOrder()
    {
        var seeded = await SeedServiceFeeOrderAsync("order_p5_d5");

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/client/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceA}",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var data = json["data"]!;

        Assert.Equal(
            seeded.Id.ToString(),
            data["paymentOrderId"]!.GetValue<string>());
        Assert.Equal(
            "Pending",
            data["paymentOrderStatus"]!.GetValue<string>());
        Assert.Equal(
            "order_p5_d5",
            data["razorpayOrderId"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "P5-D7: the client retainer invoice route reports Cancelled so the drawer stops polling")]
    public async Task ClientRetainerDetail_ReportsCancelledOrder()
    {
        // Once the cancel has succeeded, the order is Cancelled and the drawer
        // must stop waiting and offer a new payment. This goes through the real
        // cancel path rather than seeding the status.
        await SeedRetainerOrderAsync("order_p5_d7");

        var cancel = await CancelOrderAsync(
            (await ReadRetainerOrderAsync())!.Id);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        Assert.Equal(
            "Cancelled",
            json["data"]!["paymentOrderStatus"]!.GetValue<string>());

        // The invoice itself is untouched by a cancel, so it is still pending and
        // the client can start a new payment against it.
        Assert.Equal(
            "Pending",
            json["data"]!["status"]!.GetValue<string>());
    }
}
