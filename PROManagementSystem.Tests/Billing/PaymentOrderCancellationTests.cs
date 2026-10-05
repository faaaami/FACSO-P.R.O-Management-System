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
/// P5: payment-order cancellation and invoice voiding must retire the GATEWAY
/// order, not just the local row.
///
/// Before this suite, cancelling an order left a live Razorpay order behind a
/// locally cancelled row, and voiding an invoice never touched its order at
/// all. In both cases a client could still complete checkout, the gateway
/// would capture the money, and the webhook would then reject the payment
/// because the local order/invoice was no longer payable - so the capture was
/// never recorded anywhere.
///
/// These tests pin three things:
/// the gateway is closed before the local state is written, a gateway close
/// failure aborts the whole operation and leaves both sides untouched, and
/// the local write is atomic with the gateway decision.
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
    // Cancel: gateway must be closed
    // ==================================================================

    [Fact(DisplayName =
        "P5-C1: cancelling a Pending order closes the Razorpay order")]
    public async Task Cancel_ClosesGatewayOrder()
    {
        var seeded = await SeedRetainerOrderAsync("order_p5_c1");

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The gateway close happened, for exactly this order.
        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);
        Assert.Equal(
            "order_p5_c1", FakeRazorpayService.LastClosedOrderId);

        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Equal(1, await CancelAuditCountAsync(seeded.Id));
    }

    [Fact(DisplayName =
        "P5-C2: a gateway close failure aborts the cancel and leaves the order open")]
    public async Task Cancel_AbortsWhenGatewayCloseFails()
    {
        var seeded = await SeedRetainerOrderAsync("order_p5_c2");

        FakeRazorpayService.FailNextCloseOrder();

        var response = await CancelOrderAsync(seeded.Id);

        // The request must not report success: the order was never cancelled.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);

        // The transaction rolled back, so the order is still live and payable.
        var stored = await ReadRetainerOrderAsync();
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Pending, stored!.Status);
        Assert.Null(stored.CancelledAt);

        // And the invoice is untouched, so the client can still pay it.
        Assert.Equal(
            RetainerInvoiceStatus.Pending, await RetainerStatusAsync());
        Assert.Equal(0, await CancelAuditCountAsync(seeded.Id));
    }

    [Fact(DisplayName =
        "P5-C3: cancelling succeeds when the gateway order is already closed")]
    public async Task Cancel_SucceedsWhenGatewayAlreadyClosed()
    {
        // Models a retry after a lost response: the gateway end state already
        // holds, so the local cancellation must still be recorded.
        var seeded = await SeedRetainerOrderAsync("order_p5_c3");
        FakeRazorpayService.MarkGatewayOrderClosed("order_p5_c3");

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
    }

    [Fact(DisplayName =
        "P5-C4: cancelling a Created order with no gateway order makes no gateway call")]
    public async Task Cancel_CreatedOrderWithoutGatewayOrder_SkipsGateway()
    {
        // A Created order with no gateway id never reached the provider, so
        // there is nothing to close and no gateway call should be attempted.
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
        Assert.Equal(0, FakeRazorpayService.CloseOrderCallCount);

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Cancelled, stored!.Status);
    }

    [Fact(DisplayName =
        "P5-C5: cancelling a Paid order is 409 and never closes the gateway")]
    public async Task Cancel_PaidOrder_Is409()
    {
        var seeded = await SeedRetainerOrderAsync(
            "order_p5_c5", PaymentStatus.Paid);

        var response = await CancelOrderAsync(seeded.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, FakeRazorpayService.CloseOrderCallCount);

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
        Assert.Equal(0, FakeRazorpayService.CloseOrderCallCount);

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
    // Void: must retire the order AND the gateway
    // ==================================================================

    [Fact(DisplayName =
        "P5-V1: voiding a retainer invoice cancels its order and closes the gateway")]
    public async Task VoidRetainer_ClosesGatewayAndCancelsOrder()
    {
        var seeded = await SeedRetainerOrderAsync("order_p5_v1");

        var response = await VoidRetainerAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);
        Assert.Equal(
            "order_p5_v1", FakeRazorpayService.LastClosedOrderId);

        // The dangerous pre-P5 state - a live gateway order over a voided
        // invoice - is exactly what this asserts against.
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
        "P5-V2: a gateway close failure aborts the void and leaves invoice and order open")]
    public async Task VoidRetainer_AbortsWhenGatewayCloseFails()
    {
        await SeedRetainerOrderAsync("order_p5_v2");

        FakeRazorpayService.FailNextCloseOrder();

        var response = await VoidRetainerAsync();

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);

        // Both sides rolled back together: no half-voided state, and the
        // order is still live so the client can still pay it.
        Assert.Equal(
            RetainerInvoiceStatus.Pending, await RetainerStatusAsync());

        var stored = await ReadRetainerOrderAsync();
        Assert.Equal(PaymentStatus.Pending, stored!.Status);
        Assert.Null(stored.CancelledAt);
        Assert.Equal(0, await CancelAuditCountAsync(stored.Id));
    }

    [Fact(DisplayName =
        "P5-V3: voiding an invoice with no payment order still voids and makes no gateway call")]
    public async Task VoidRetainer_NoOrder_StillVoids()
    {
        // Regression guard: the new order handling must not break the common
        // case of voiding an invoice that was never checked out.
        var response = await VoidRetainerAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, FakeRazorpayService.CloseOrderCallCount);
        Assert.Equal(
            RetainerInvoiceStatus.Void, await RetainerStatusAsync());
    }

    [Fact(DisplayName =
        "P5-V4: voiding a paid invoice is 409 and never closes the gateway")]
    public async Task VoidRetainer_PaidInvoice_Is409()
    {
        await SeedRetainerOrderAsync("order_p5_v4");
        await SetRetainerStatusAsync(RetainerInvoiceStatus.Paid);

        var response = await VoidRetainerAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, FakeRazorpayService.CloseOrderCallCount);

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
            // The void won: the order must have been closed and cancelled, so
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
        "P5-V6: voiding a service fee invoice cancels its order and closes the gateway")]
    public async Task VoidServiceFee_ClosesGatewayAndCancelsOrder()
    {
        var seeded = await SeedServiceFeeOrderAsync("order_p5_v6");

        var response = await VoidServiceFeeAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);
        Assert.Equal(
            "order_p5_v6", FakeRazorpayService.LastClosedOrderId);

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
        "P5-V7: a gateway close failure aborts a service fee void and leaves both sides open")]
    public async Task VoidServiceFee_AbortsWhenGatewayCloseFails()
    {
        await SeedServiceFeeOrderAsync("order_p5_v7");

        FakeRazorpayService.FailNextCloseOrder();

        var response = await VoidServiceFeeAsync();

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, FakeRazorpayService.CloseOrderCallCount);

        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, await ServiceFeeStatusAsync());

        var stored = await ReadServiceFeeOrderAsync();
        Assert.Equal(PaymentStatus.Pending, stored!.Status);
        Assert.Null(stored.CancelledAt);
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
        "P5-D6: a failed gateway close leaves the order Pending, so the drawer keeps waiting")]
    public async Task ClientRetainerDetail_FailedCloseStillReportsPendingOrder()
    {
        // The confirming state needs to distinguish "still confirming" from
        // "cancelled, do not wait any longer". A refused close is the first of
        // those: the payment may yet arrive, so the drawer must not be told the
        // payment died.
        await SeedRetainerOrderAsync("order_p5_d6");

        FakeRazorpayService.FailNextCloseOrder();

        Assert.NotEqual(
            HttpStatusCode.OK,
            (await CancelOrderAsync(
                (await ReadRetainerOrderAsync())!.Id)).StatusCode);

        var response = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        // The close failed, so the order is still live: the drawer must keep
        // waiting rather than claim the payment died.
        Assert.Equal(
            "Pending",
            json["data"]!["paymentOrderStatus"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "P5-D7: the client retainer invoice route reports Cancelled so the drawer stops polling")]
    public async Task ClientRetainerDetail_ReportsCancelledOrder()
    {
        // The other half of the same decision: once the cancel has actually
        // succeeded, the order is Cancelled and the drawer must stop waiting and
        // offer a new payment. This goes through the real cancel path rather than
        // seeding the status, so it also covers the detail read of a genuine
        // gateway-closed order.
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
