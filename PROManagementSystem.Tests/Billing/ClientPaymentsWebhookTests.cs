using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Payment;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// Week 3 Day 4 - Stage 3: Razorpay webhook payment completion
/// (POST /api/v1/client/payments/webhook). The endpoint is anonymous and the
/// only gate is the verified webhook signature over the exact raw body. The
/// invoice is derived from the persisted PaymentOrder keyed by order_id - never
/// from arbitrary payload data. Completion (order Paid + invoice Paid + audit)
/// is one atomic transaction serialised by a PostgreSQL FOR UPDATE row lock.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientPaymentsWebhookTests : IntegrationTestBase
{
    public ClientPaymentsWebhookTests(DatabaseFixture fixture)
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

    private async Task<HttpResponseMessage> PostWebhook(
        string rawBody,
        string? signature = null,
        string? eventId = "evt_test_0001",
        bool anonymous = false)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/client/payments/webhook")
        {
            Content = new StringContent(
                rawBody, Encoding.UTF8, "application/json")
        };

        if (signature is not null)
        {
            request.Headers.Add("X-Razorpay-Signature", signature);
        }

        if (eventId is not null)
        {
            request.Headers.Add("X-Razorpay-Event-Id", eventId);
        }

        // Anonymous = real JWT pipeline with no token; otherwise use the
        // scope client (still effectively unauthenticated for this endpoint).
        return anonymous
            ? await AuthClient.SendAsync(request)
            : await ScopeClient.SendAsync(request);
    }

    private sealed record RetainerOrderState(
        PaymentStatus OrderStatus,
        string? RazorpayPaymentId,
        string? RazorpaySignature,
        DateTime? PaidAt,
        RetainerInvoiceStatus? InvoiceStatus,
        DateTime? InvoicePaidAt,
        int AuditCount);

    private async Task<RetainerOrderState> RetainerOrderStateAsync(
        string razorpayOrderId)
    {
        RetainerOrderState? state = null;

        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders
                .IgnoreQueryFilters()
                .First(x => x.RazorpayOrderId == razorpayOrderId);
            RetainerInvoiceStatus? invStatus = null;
            DateTime? invPaidAt = null;

            if (order.InvoiceType == PaymentInvoiceType.RetainerInvoice)
            {
                var inv = db.RetainerInvoices
                    .IgnoreQueryFilters()
                    .First(x => x.Id == order.RetainerInvoiceId!.Value);
                invStatus = inv.Status;
                invPaidAt = inv.PaidAt;
            }

            state = new RetainerOrderState(
                order.Status,
                order.RazorpayPaymentId,
                order.RazorpaySignature,
                order.PaidAt,
                invStatus,
                invPaidAt,
                db.AuditLogs.Count(x =>
                    x.Action == "PaymentCompletedFromWebhook" &&
                    x.EntityId == order.Id));
        });

        return state!;
    }

    private async Task<Guid> SeedRetainerOrder(
        string razorpayOrderId,
        PaymentStatus status = PaymentStatus.Pending)
    {
        var orderId = Guid.NewGuid();

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
                PaidAt = null,
                FailedAt = null,
                CancelledAt = null,
                Notes = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        return orderId;
    }

    private async Task<Guid> SeedServiceFeeOrder(string razorpayOrderId)
    {
        var orderId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = orderId,
                InvoiceType = PaymentInvoiceType.ServiceFeeInvoice,
                RetainerInvoiceId = null,
                ServiceFeeInvoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                Amount = 150m,
                Currency = "AED",
                Status = PaymentStatus.Pending,
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
            });
            db.SaveChanges();
        });

        return orderId;
    }

    // ==================================================================
    // Happy path
    // ==================================================================

    [Fact(DisplayName =
        "WH1: valid payment.captured completes a retainer payment order and invoice")]
    public async Task Captured_Retainer_Completes()
    {
        const string orderId = "order_tst_ret_0001";
        const string paymentId = "pay_tst_0001";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, paymentId, 250000, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = System.Text.Json.Nodes.JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        Assert.True((bool)body["data"]!["completed"]!);

        var s = await RetainerOrderStateAsync(orderId);

        Assert.Equal(PaymentStatus.Paid, s.OrderStatus);
        Assert.Equal(paymentId, s.RazorpayPaymentId);
        Assert.Equal(Signature(rawBody), s.RazorpaySignature);
        Assert.NotNull(s.PaidAt);
        Assert.Equal(RetainerInvoiceStatus.Paid, s.InvoiceStatus);
        Assert.Equal(s.PaidAt, s.InvoicePaidAt);
        Assert.Equal(1, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH2: valid payment.captured completes a service-fee payment order and invoice")]
    public async Task Captured_ServiceFee_Completes()
    {
        const string orderId = "order_tst_sf_0001";
        const string paymentId = "pay_tst_0002";
        var orderGuid = await SeedServiceFeeOrder(orderId);
        var rawBody = BuildBody(orderId, paymentId, 15000, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders.First(x => x.Id == orderGuid);
            Assert.Equal(PaymentStatus.Paid, order.Status);
            Assert.Equal(paymentId, order.RazorpayPaymentId);
            Assert.Equal(Signature(rawBody), order.RazorpaySignature);
            Assert.NotNull(order.PaidAt);

            var inv = db.ServiceFeeInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.ServiceFeeInvoiceA);
            Assert.Equal(ServiceFeeInvoiceStatus.Paid, inv.Status);
            Assert.Equal(order.PaidAt, inv.PaidAt);

            var audit = db.AuditLogs.Count(x =>
                x.Action == "PaymentCompletedFromWebhook" &&
                x.EntityId == orderGuid);
            Assert.Equal(1, audit);
        });
    }

    // ==================================================================
    // Signature / body failures
    // ==================================================================

    [Fact(DisplayName =
        "WH3: invalid signature is rejected with no state change")]
    public async Task InvalidSignature_Rejected()
    {
        const string orderId = "order_tst_ret_0002";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, "pay_tst_0003", 250000, "AED");

        var response = await PostWebhook(rawBody, "0" + Signature(rawBody).Substring(1));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Null(s.PaidAt);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH4: missing signature header is rejected")]
    public async Task MissingSignature_Rejected()
    {
        const string orderId = "order_tst_ret_0003";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, "pay_tst_0004", 250000, "AED");

        var response = await PostWebhook(rawBody, signature: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Null(s.PaidAt);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH5: malformed body returns 400 and does not change state")]
    public async Task MalformedBody_BadRequest()
    {
        const string orderId = "order_tst_ret_0004";
        var orderGuid = await SeedRetainerOrder(orderId);

        // Valid signature over the malformed body so it reaches JSON parsing.
        const string malformed = "this is not json";
        var response = await PostWebhook(malformed, Signature(malformed));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var dbOk = false;
        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders.First(x => x.Id == orderGuid);
            dbOk = order.Status == PaymentStatus.Pending;
        });
        Assert.True(dbOk);
    }

    [Fact(DisplayName =
        "WH6: payload missing payment entity returns 400")]
    public async Task MissingPaymentEntity_BadRequest()
    {
        const string rawBody = "{\"event\":\"payment.captured\",\"payload\":{}}";
        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================================================================
    // Order / invoice state failures
    // ==================================================================

    [Fact(DisplayName =
        "WH7: unknown Razorpay order applies no payment")]
    public async Task UnknownOrder_NotFound()
    {
        var orderGuid = await SeedRetainerOrder("order_tst_ret_0005");
        const string unknownOrder = "order_does_not_exist";
        var rawBody = BuildBody(unknownOrder, "pay_tst_0005", 250000, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var s = await RetainerOrderStateAsync("order_tst_ret_0005");
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH8: soft-deleted PaymentOrder applies no payment")]
    public async Task DeletedOrder_NotFound()
    {
        const string orderId = "order_tst_ret_0006";
        var orderGuid = await SeedRetainerOrder(orderId);

        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders.IgnoreQueryFilters().First(x => x.Id == orderGuid);
            order.IsDeleted = true;
            order.DeletedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var rawBody = BuildBody(orderId, "pay_tst_0006", 250000, "AED");
        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await WithScopeDbContextAsync(db =>
            Assert.Equal(
                RetainerInvoiceStatus.Pending,
                db.RetainerInvoices.IgnoreQueryFilters()
                    .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA)
                    .Status));
    }

    [Fact(DisplayName =
        "WH9: missing invoice applies no payment (404)")]
    public async Task MissingInvoice_NoPayment()
    {
        const string orderId = "order_tst_ret_0007";
        await SeedRetainerOrder(orderId);

        // Soft-delete the linked retainer invoice so it no longer resolves.
        await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
            inv.IsDeleted = true;
            inv.DeletedAt = DateTime.UtcNow;
            inv.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var rawBody = BuildBody(orderId, "pay_tst_0007", 250000, "AED");
        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Null(s.PaidAt);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH10: void invoice is rejected (409) with no state change")]
    public async Task VoidInvoice_Rejected()
    {
        const string orderId = "order_tst_ret_0008";
        await SeedRetainerOrder(orderId);

        await WithScopeDbContextAsync(db =>
        {
            var inv = db.RetainerInvoices
                .IgnoreQueryFilters()
                .First(x => x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
            inv.Status = RetainerInvoiceStatus.Void;
            inv.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var rawBody = BuildBody(orderId, "pay_tst_0008", 250000, "AED");
        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Null(s.PaidAt);
        Assert.Equal(RetainerInvoiceStatus.Void, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH11: non-pending (paid) invoice already reflected in completed order is idempotent")]
    public async Task AlreadyCompleted_IdempotentNoOp()
    {
        const string orderId = "order_tst_ret_0009";
        const string paymentId = "pay_tst_0009";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, paymentId, 250000, "AED");

        var first = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Record PaidAt from the first completion.
        var firstState = await RetainerOrderStateAsync(orderId);

        var second = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);

        Assert.Equal(PaymentStatus.Paid, s.OrderStatus);
        Assert.Equal(paymentId, s.RazorpayPaymentId);
        // No second financial transition, no modified PaidAt, one audit log.
        Assert.Equal(s.PaidAt, firstState.PaidAt);
        Assert.Equal(s.InvoicePaidAt, firstState.PaidAt);
        Assert.Equal(1, s.AuditCount);
        Assert.Equal(firstState.AuditCount, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH12: cancelled payment order is rejected (409)")]
    public async Task CancelledOrder_Rejected()
    {
        const string orderId = "order_tst_ret_0010";
        var orderGuid = await SeedRetainerOrder(orderId, PaymentStatus.Cancelled);
        var rawBody = BuildBody(orderId, "pay_tst_0010", 250000, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Cancelled, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Theory(DisplayName =
        "WH13: failed/refunded payment order is rejected (409)")]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task FailedOrRefundedOrder_Rejected(PaymentStatus status)
    {
        const string orderId = "order_tst_ret_0011";
        var orderGuid = await SeedRetainerOrder(orderId, status);
        var rawBody = BuildBody(orderId, "pay_tst_0011", 250000, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(status, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH14: non-captured payment status does not mark paid")]
    public async Task NonCapturedStatus_NotPaid()
    {
        const string orderId = "order_tst_ret_0012";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(
            orderId, "pay_tst_0012", 250000, "AED",
            status: "authorized");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH15: wrong event name does not mark paid")]
    public async Task WrongEvent_NotMarkedPaid()
    {
        const string orderId = "order_tst_ret_0013";
        await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(
            orderId, "pay_tst_0013", 250000, "AED",
            evt: "payment.failed");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH16: amount mismatch is rejected (409) with no state change")]
    public async Task AmountMismatch_Rejected()
    {
        const string orderId = "order_tst_ret_0014";
        var orderGuid = await SeedRetainerOrder(orderId);
        // Order amount = 2500 -> 250000 paise, but send a different amount.
        var rawBody = BuildBody(orderId, "pay_tst_0014", 999999, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH17: currency mismatch is rejected (409) with no state change")]
    public async Task CurrencyMismatch_Rejected()
    {
        const string orderId = "order_tst_ret_0015";
        await SeedRetainerOrder(orderId);
        // Order currency = AED, but send a different currency.
        var rawBody = BuildBody(orderId, "pay_tst_0015", 250000, "USD");

        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var s = await RetainerOrderStateAsync(orderId);
        Assert.Equal(PaymentStatus.Pending, s.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Pending, s.InvoiceStatus);
        Assert.Equal(0, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH18: payment order invoice-type mismatch is rejected (409)")]
    public async Task InvoiceTypeMismatch_Rejected()
    {
        const string orderId = "order_tst_ret_0016";
        var orderGuid = Guid.NewGuid();

        // InvoiceType says ServiceFee but only RetainerInvoiceId is set - an
        // inconsistent payment order that must not complete.
        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = orderGuid,
                InvoiceType = PaymentInvoiceType.ServiceFeeInvoice,
                RetainerInvoiceId = TestDataSeeder.Ids.RetainerInvoiceA,
                ServiceFeeInvoiceId = null,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                Amount = 150m,
                Currency = "AED",
                Status = PaymentStatus.Pending,
                RazorpayOrderId = orderId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var rawBody = BuildBody(orderId, "pay_tst_0016", 15000, "AED");
        var response = await PostWebhook(rawBody, Signature(rawBody));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders.First(x => x.Id == orderGuid);
            Assert.Equal(PaymentStatus.Pending, order.Status);
        });
    }

    // ==================================================================
    // Idempotency / concurrency
    // ==================================================================

    [Fact(DisplayName =
        "WH19: duplicate delivered webhook yields one financial transition and one audit log")]
    public async Task DuplicateWebhook_OneTransition_OneAudit()
    {
        const string orderId = "order_tst_ret_0017";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, "pay_tst_0017", 250000, "AED");

        await PostWebhook(rawBody, Signature(rawBody));
        await PostWebhook(rawBody, Signature(rawBody));
        await PostWebhook(rawBody, Signature(rawBody));

        var s = await RetainerOrderStateAsync(orderId);

        Assert.Equal(PaymentStatus.Paid, s.OrderStatus);
        Assert.Equal(s.PaidAt, s.InvoicePaidAt);
        Assert.Equal(1, s.AuditCount);
    }

    [Fact(DisplayName =
        "WH20: concurrent duplicate delivery allows only one successful transition")]
    public async Task ConcurrentDuplicate_OneTransition()
    {
        const string orderId = "order_tst_ret_0018";
        const string paymentId = "pay_tst_0018";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, paymentId, 250000, "AED");

        var sig = Signature(rawBody);

        var results = await Task.WhenAll(
            Task.Run(() => PostWebhook(rawBody, sig)),
            Task.Run(() => PostWebhook(rawBody, sig)),
            Task.Run(() => PostWebhook(rawBody, sig)));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var s = await RetainerOrderStateAsync(orderId);

        Assert.Equal(PaymentStatus.Paid, s.OrderStatus);
        Assert.Equal(paymentId, s.RazorpayPaymentId);
        Assert.Equal(s.PaidAt, s.InvoicePaidAt);
        Assert.Equal(RetainerInvoiceStatus.Paid, s.InvoiceStatus);
        Assert.Equal(1, s.AuditCount);
    }

    // ==================================================================
    // Security / auth
    // ==================================================================

    [Fact(DisplayName =
        "WH21: anonymous valid webhook is accepted (200)")]
    public async Task Anonymous_ValidWebhook_Accepted()
    {
        const string orderId = "order_tst_ret_0019";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, "pay_tst_0019", 250000, "AED");

        var response = await PostWebhook(rawBody, Signature(rawBody), anonymous: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WithScopeDbContextAsync(db =>
            Assert.Equal(
                PaymentStatus.Paid,
                db.PaymentOrders.First(x => x.Id == orderGuid).Status));
    }

    [Fact(DisplayName =
        "WH22: unrelated/unauthenticated request without a token is rejected (401)")]
    public async Task Anonymous_NoSignature_Rejected()
    {
        const string orderId = "order_tst_ret_0020";
        var orderGuid = await SeedRetainerOrder(orderId);
        var rawBody = BuildBody(orderId, "pay_tst_0020", 250000, "AED");

        var response = await PostWebhook(rawBody, signature: null, anonymous: true);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await WithScopeDbContextAsync(db =>
            Assert.Equal(
                PaymentStatus.Pending,
                db.PaymentOrders.First(x => x.Id == orderGuid).Status));
    }

    // ==================================================================
    // G4 - a reused order completes through the unchanged webhook path
    // ==================================================================

    private async Task<HttpResponseMessage> CreateOrderAsClientAsync(
        Guid invoiceId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/client/payments/create-order")
        {
            Content = JsonContent.Create(new
            {
                invoiceType = "Retainer",
                invoiceId
            })
        };

        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                TestDataSeeder.Ids.ClientUserA,
                "Client",
                TestDataSeeder.Ids.CompanyA.ToString()));

        return await ScopeClient.SendAsync(request);
    }

    [Fact(DisplayName =
        "G4-WH1: a reused payment order completes through the existing webhook and stays idempotent")]
    public async Task ReusedOrder_CompletesThroughWebhook()
    {
        // 1. First attempt creates the order and its gateway order.
        var created = await CreateOrderAsClientAsync(
            TestDataSeeder.Ids.RetainerInvoiceA);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var createdBody = JsonNode.Parse(
            await created.Content.ReadAsStringAsync())!["data"]!;

        var orderId = createdBody["id"]!.ToString();
        var razorpayOrderId = (string)createdBody["razorpayOrderId"]!;
        var razorpayAmount = (int)createdBody["razorpayAmount"]!;

        Assert.NotNull(razorpayOrderId);
        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);

        // 2. The client abandons checkout and pays again: the SAME order and
        //    the SAME gateway order id come back, with no second gateway call.
        var reused = await CreateOrderAsClientAsync(
            TestDataSeeder.Ids.RetainerInvoiceA);

        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);

        var reusedBody = JsonNode.Parse(
            await reused.Content.ReadAsStringAsync())!["data"]!;

        Assert.Equal(orderId, reusedBody["id"]!.ToString());
        Assert.Equal(
            razorpayOrderId, (string)reusedBody["razorpayOrderId"]!);
        Assert.Equal(razorpayAmount, (int)reusedBody["razorpayAmount"]!);
        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);

        // 3. The reused gateway order completes through the unchanged path.
        var rawBody = BuildBody(
            razorpayOrderId, "pay_tst_reuse_01", razorpayAmount, "AED");

        var completed = await PostWebhook(
            rawBody, Signature(rawBody));

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var state = await RetainerOrderStateAsync(razorpayOrderId);

        Assert.Equal(PaymentStatus.Paid, state.OrderStatus);
        Assert.Equal(RetainerInvoiceStatus.Paid, state.InvoiceStatus);
        Assert.Equal(state.PaidAt, state.InvoicePaidAt);

        // 4. A repeated webhook is still an idempotent no-op.
        var repeated = await PostWebhook(
            rawBody, Signature(rawBody));

        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        var afterRepeat = await RetainerOrderStateAsync(razorpayOrderId);

        Assert.Equal(PaymentStatus.Paid, afterRepeat.OrderStatus);
        Assert.Equal(state.PaidAt, afterRepeat.PaidAt);
        Assert.Equal(1, afterRepeat.AuditCount);
    }
}
