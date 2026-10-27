using System.Net;
using System.Net.Http.Json;
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
/// Review item C2: an invoice must stay payable after a payment order is
/// cancelled or failed. An invoice may have many payment orders over time but
/// at most one open (Created/Pending) order, enforced by filtered unique
/// indexes. Manual settlement is allowed when the latest order is closed, and
/// cancels a still-open order in the same transaction.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class InvoicePayableAfterOrderClosedTests : IntegrationTestBase
{
    private const string ClientCreateOrderUrl =
        "/api/v1/client/payments/create-order";

    private const string WebhookUrl =
        "/api/v1/client/payments/webhook";

    public InvoicePayableAfterOrderClosedTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private Task<HttpResponseMessage> ClientCreateRetainerOrderAsync()
    {
        return ScopeClient.SendAsync(ScopePost(
            ClientCreateOrderUrl,
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            },
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));
    }

    private Task<HttpResponseMessage> AdminCreateServiceFeeOrderAsync()
    {
        return ScopeClient.SendAsync(ScopePost(
            "/api/v1/payment-orders",
            new
            {
                invoiceType = "ServiceFeeInvoice",
                serviceFeeInvoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    private Task<HttpResponseMessage> AdminCancelOrderAsync(Guid orderId)
    {
        return ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/payment-orders/{orderId}/cancel",
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    private Task<HttpResponseMessage> AdminMarkRetainerPaidAsync()
    {
        return ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}/mark-paid",
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    private Task<HttpResponseMessage> AdminMarkServiceFeePaidAsync()
    {
        return ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceA}/mark-paid",
            body: null,
            TestDataSeeder.Ids.Admin,
            "Admin"));
    }

    private static async Task<(Guid Id, string RazorpayOrderId)> ReadOrderAsync(
        HttpResponseMessage response)
    {
        var data = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!["data"]!;

        return (Guid.Parse(data["id"]!.ToString()),
            (string)data["razorpayOrderId"]!);
    }

    private async Task<List<PaymentOrder>> RetainerOrdersAsync()
    {
        List<PaymentOrder> orders = new();

        await WithScopeDbContextAsync(db =>
        {
            orders = db.PaymentOrders
                .AsNoTracking()
                .Where(x => x.RetainerInvoiceId == TestDataSeeder.Ids.RetainerInvoiceA)
                .OrderBy(x => x.CreatedAt)
                .ToList();
        });

        return orders;
    }

    private async Task<RetainerInvoice> RetainerInvoiceAsync()
    {
        RetainerInvoice? invoice = null;

        await WithScopeDbContextAsync(db =>
        {
            invoice = db.RetainerInvoices
                .AsNoTracking()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
        });

        return invoice!;
    }

    private async Task SeedRetainerOrderAsync(
        PaymentStatus status,
        string razorpayOrderId)
    {
        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(NewRetainerOrder(status, razorpayOrderId));
            db.SaveChanges();
        });
    }

    private static PaymentOrder NewRetainerOrder(
        PaymentStatus status,
        string razorpayOrderId) => new()
    {
        Id = Guid.NewGuid(),
        InvoiceType = PaymentInvoiceType.RetainerInvoice,
        RetainerInvoiceId = TestDataSeeder.Ids.RetainerInvoiceA,
        ClientCompanyId = TestDataSeeder.Ids.CompanyA,
        Amount = 2500m,
        Currency = "AED",
        Status = status,
        RazorpayOrderId = razorpayOrderId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private Task<HttpResponseMessage> PostCapturedWebhookAsync(
        string razorpayOrderId,
        string paymentId,
        string eventId)
    {
        var rawBody =
            "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{" +
            "\"id\":\"" + paymentId + "\"," +
            "\"order_id\":\"" + razorpayOrderId + "\"," +
            "\"amount\":250000,\"currency\":\"AED\",\"status\":\"captured\"}}}}";

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(FakeRazorpayService.FakeWebhookSecret));

        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            "X-Razorpay-Signature",
            Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
                .ToLowerInvariant());
        request.Headers.Add("X-Razorpay-Event-Id", eventId);

        return ScopeClient.SendAsync(request);
    }

    // ==================================================================
    // Cancel -> pay online again
    // ==================================================================

    [Fact(DisplayName =
        "C2-1: after a cancelled order the client can pay online again with a new order")]
    public async Task CancelledOrder_ClientCanPayOnlineAgain()
    {
        var first = await ClientCreateRetainerOrderAsync();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var (firstId, firstGatewayId) = await ReadOrderAsync(first);

        var cancel = await AdminCancelOrderAsync(firstId);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var second = await ClientCreateRetainerOrderAsync();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var (secondId, secondGatewayId) = await ReadOrderAsync(second);

        // A brand-new order and gateway order; the cancelled one is untouched.
        Assert.NotEqual(firstId, secondId);
        Assert.NotEqual(firstGatewayId, secondGatewayId);
        Assert.Equal(2, FakeRazorpayService.CreateOrderCallCount);

        var orders = await RetainerOrdersAsync();
        Assert.Equal(2, orders.Count);
        Assert.Equal(PaymentStatus.Cancelled, orders.Single(x => x.Id == firstId).Status);
        Assert.Equal(PaymentStatus.Pending, orders.Single(x => x.Id == secondId).Status);

        // The new order completes the invoice through the webhook.
        var webhook = await PostCapturedWebhookAsync(
            secondGatewayId, "pay_c2_0001", "evt_c2_0001");
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var invoice = await RetainerInvoiceAsync();
        Assert.Equal(RetainerInvoiceStatus.Paid, invoice.Status);

        orders = await RetainerOrdersAsync();
        Assert.Equal(PaymentStatus.Paid, orders.Single(x => x.Id == secondId).Status);
        Assert.Equal(PaymentStatus.Cancelled, orders.Single(x => x.Id == firstId).Status);
    }

    [Fact(DisplayName =
        "C2-2: a service fee invoice gets a new admin order after its order was cancelled")]
    public async Task CancelledServiceFeeOrder_AdminCanRaiseNewOrder()
    {
        var first = await AdminCreateServiceFeeOrderAsync();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var (firstId, _) = await ReadOrderAsync(first);

        Assert.Equal(HttpStatusCode.OK, (await AdminCancelOrderAsync(firstId)).StatusCode);

        var second = await AdminCreateServiceFeeOrderAsync();
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var (secondId, _) = await ReadOrderAsync(second);

        Assert.NotEqual(firstId, secondId);
    }

    [Fact(DisplayName =
        "C2-3: after a failed order the client can pay online again with a new order")]
    public async Task FailedOrder_ClientCanPayOnlineAgain()
    {
        await SeedRetainerOrderAsync(PaymentStatus.Failed, "order_c2_failed");

        var response = await ClientCreateRetainerOrderAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);

        var orders = await RetainerOrdersAsync();
        Assert.Equal(2, orders.Count);
        Assert.Single(orders, x => x.Status == PaymentStatus.Pending);
    }

    [Fact(DisplayName =
        "C2-3b: a payment held for review blocks a new online payment so the client is not charged twice")]
    public async Task PaidRequiresReviewOrder_BlocksNewOrder()
    {
        // e.g. an amount mismatch: the money was captured but not applied, so
        // the invoice is still Pending. Another checkout would charge again.
        await SeedRetainerOrderAsync(
            PaymentStatus.PaidRequiresReview, "order_c2_review");

        var response = await ClientCreateRetainerOrderAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, FakeRazorpayService.CreateOrderCallCount);
        Assert.Single(await RetainerOrdersAsync());
        Assert.Equal(
            RetainerInvoiceStatus.Pending, (await RetainerInvoiceAsync()).Status);
    }

    // ==================================================================
    // Cancel -> mark paid manually
    // ==================================================================

    [Fact(DisplayName =
        "C2-4: after a cancelled order the admin can mark the invoice paid manually")]
    public async Task CancelledOrder_AdminCanMarkPaid()
    {
        var created = await ClientCreateRetainerOrderAsync();
        var (orderId, _) = await ReadOrderAsync(created);
        Assert.Equal(HttpStatusCode.OK, (await AdminCancelOrderAsync(orderId)).StatusCode);

        var markPaid = await AdminMarkRetainerPaidAsync();

        Assert.Equal(HttpStatusCode.OK, markPaid.StatusCode);

        var invoice = await RetainerInvoiceAsync();
        Assert.Equal(RetainerInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);

        // The cancelled order stays a cancelled record: no PaidAt, no payment.
        var order = (await RetainerOrdersAsync()).Single();
        Assert.Equal(PaymentStatus.Cancelled, order.Status);
        Assert.Null(order.PaidAt);
    }

    [Fact(DisplayName =
        "C2-5: a failed order does not block a manual mark-paid of a service fee invoice")]
    public async Task FailedServiceFeeOrder_AdminCanMarkPaid()
    {
        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = Guid.NewGuid(),
                InvoiceType = PaymentInvoiceType.ServiceFeeInvoice,
                ServiceFeeInvoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                Amount = 150m,
                Currency = "AED",
                Status = PaymentStatus.Failed,
                RazorpayOrderId = "order_c2_sf_failed",
                FailedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });

        var response = await AdminMarkServiceFeePaidAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                ServiceFeeInvoiceStatus.Paid,
                db.ServiceFeeInvoices.First(x => x.Id == TestDataSeeder.Ids.ServiceFeeInvoiceA).Status);
            Assert.Equal(
                PaymentStatus.Failed,
                db.PaymentOrders.Single(x => x.ServiceFeeInvoiceId == TestDataSeeder.Ids.ServiceFeeInvoiceA).Status);
        });
    }

    [Fact(DisplayName =
        "C2-6: marking paid cancels a still-open order in the same transaction")]
    public async Task MarkPaid_CancelsOpenOrder()
    {
        var created = await ClientCreateRetainerOrderAsync();
        var (orderId, _) = await ReadOrderAsync(created);

        var markPaid = await AdminMarkRetainerPaidAsync();

        Assert.Equal(HttpStatusCode.OK, markPaid.StatusCode);

        var order = (await RetainerOrdersAsync()).Single();
        Assert.Equal(PaymentStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAt);
        Assert.Null(order.PaidAt);

        await WithScopeDbContextAsync(db =>
        {
            var audit = db.AuditLogs.Single(x =>
                x.EntityId == orderId && x.Action == "PaymentOrderCancelled");
            Assert.Equal(TestDataSeeder.Ids.Admin, audit.UserId);
        });
    }

    // ==================================================================
    // One open order per invoice
    // ==================================================================

    [Fact(DisplayName =
        "C2-8: concurrent create-order requests after a cancel leave exactly one open order")]
    public async Task ConcurrentCreates_AfterCancel_OneOpenOrder()
    {
        var first = await ClientCreateRetainerOrderAsync();
        var (firstId, _) = await ReadOrderAsync(first);
        Assert.Equal(HttpStatusCode.OK, (await AdminCancelOrderAsync(firstId)).StatusCode);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ =>
                Task.Run(ClientCreateRetainerOrderAsync)));

        // Losers either adopt the winner's order (200) or are told to retry
        // (409) while the winner is still talking to the gateway.
        Assert.All(responses, r => Assert.Contains(
            r.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));

        var okIds = new HashSet<Guid>();
        foreach (var ok in responses.Where(r => r.StatusCode == HttpStatusCode.OK))
        {
            okIds.Add((await ReadOrderAsync(ok)).Id);
        }

        Assert.Single(okIds);

        var orders = await RetainerOrdersAsync();
        var open = orders.Where(x =>
            x.Status is PaymentStatus.Created or PaymentStatus.Pending).ToList();

        Assert.Single(open);
        Assert.Equal(okIds.Single(), open[0].Id);
        Assert.Equal(2, orders.Count);
    }

    [Fact(DisplayName =
        "C2-9: the database allows many closed orders but rejects a second open order for one invoice")]
    public async Task Database_RejectsSecondOpenOrder()
    {
        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(NewRetainerOrder(PaymentStatus.Cancelled, "order_c2_x1"));
            db.PaymentOrders.Add(NewRetainerOrder(PaymentStatus.Failed, "order_c2_x2"));
            db.PaymentOrders.Add(NewRetainerOrder(PaymentStatus.Pending, "order_c2_x3"));
            db.SaveChanges();
        });

        DbUpdateException? error = null;

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(NewRetainerOrder(PaymentStatus.Created, "order_c2_x4"));
            error = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        });

        Assert.Contains(
            "IX_payment_orders_RetainerInvoiceId_Open",
            error!.InnerException?.Message ?? error.Message);
    }

    [Fact(DisplayName =
        "C2-10: the client invoice detail shows the newest order after a cancel and re-pay")]
    public async Task InvoiceDetail_ShowsLatestOrder()
    {
        var first = await ClientCreateRetainerOrderAsync();
        var (firstId, _) = await ReadOrderAsync(first);
        await AdminCancelOrderAsync(firstId);
        var second = await ClientCreateRetainerOrderAsync();
        var (secondId, secondGatewayId) = await ReadOrderAsync(second);

        var detail = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}",
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var data = JsonNode.Parse(await detail.Content.ReadAsStringAsync())!["data"]!;
        Assert.Equal(secondId.ToString(), data["paymentOrderId"]!.ToString());
        Assert.Equal(secondGatewayId, (string)data["razorpayOrderId"]!);
    }
}
