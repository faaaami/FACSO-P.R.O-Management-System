using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// Week 3 Day 4 - Stage 2: client-facing billing, payment and service-report
/// endpoints on /api/v1/client. The tenant is always derived from the
/// authenticated client's token (client_company_id claim) and never accepted
/// from the request. Lists and single-resource reads must isolate to the
/// caller's own company; cross-company access surfaces as 404 (not 403) for
/// reads, while cross-company create-order rejects with 403 at the handler.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientBillingApiTests : IntegrationTestBase
{
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid ClientUserB = TestDataSeeder.Ids.ClientUserB;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid CompanyB = TestDataSeeder.Ids.CompanyB;
    private static readonly Guid Unknown = TestDataSeeder.Ids.Unknown;

    public ClientBillingApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private async Task<HttpResponseMessage> ClientGet(
        string url,
        Guid userId = default,
        Guid? clientCompanyId = null)
    {
        userId = userId == default ? ClientUserA : userId;
        clientCompanyId ??= CompanyA;
        return await ScopeClient.SendAsync(
            ScopeGet(url, userId, "Client", clientCompanyId.ToString()));
    }

    private async Task<HttpResponseMessage> ClientPost(
        string url,
        object? body,
        Guid userId = default,
        Guid? clientCompanyId = null)
    {
        userId = userId == default ? ClientUserA : userId;
        clientCompanyId ??= CompanyA;
        return await ScopeClient.SendAsync(
            ScopePost(url, body, userId, "Client",
                clientCompanyId.ToString()));
    }

    private static async Task<JsonArray?> DataItemsAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        return body["data"]!["items"]!.AsArray();
    }

    private static List<Guid> IdsOf(JsonArray items)
        => items
            .Select(x => Guid.Parse((string)x!["id"]!))
            .ToList();

    private static async Task<string> ErrorCodeAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        return (string)body["error"]!["code"]!;
    }

    private static async Task<JsonNode> DataNodeAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        return body["data"]!;
    }

    // ==================================================================
    // G4 helpers - payment order resume
    // ==================================================================

    private sealed record OrderState(
        Guid Id,
        decimal Amount,
        string Currency,
        PaymentStatus Status,
        string? RazorpayOrderId,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    /// <summary>
    /// Seeds a non-deleted PaymentOrder with fixed timestamps so a test can
    /// assert that the reuse path leaves them untouched. The default amount
    /// (3000) and currency differ from the seeded invoice (2500 AED) so tests
    /// can prove values come from the order and never from the invoice.
    /// </summary>
    private async Task<OrderState> SeedOrderAsync(
        Guid? retainerInvoiceId = null,
        Guid? serviceFeeInvoiceId = null,
        PaymentStatus status = PaymentStatus.Pending,
        string? razorpayOrderId = FakeRazorpayService.FakeOrderId,
        decimal amount = 3000m,
        string currency = "AED")
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 5, 10, 0, 0,
            DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 1, 6, 11, 0, 0,
            DateTimeKind.Utc);

        await WithScopeDbContextAsync(db =>
        {
            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = id,
                InvoiceType = retainerInvoiceId.HasValue
                    ? PaymentInvoiceType.RetainerInvoice
                    : PaymentInvoiceType.ServiceFeeInvoice,
                RetainerInvoiceId = retainerInvoiceId,
                ServiceFeeInvoiceId = serviceFeeInvoiceId,
                ClientCompanyId = CompanyA,
                Amount = amount,
                Currency = currency,
                Status = status,
                RazorpayOrderId = razorpayOrderId,
                RazorpayPaymentId = null,
                RazorpaySignature = null,
                Notes = null,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return new OrderState(
            id, amount, currency, status,
            razorpayOrderId, createdAt, updatedAt);
    }

    private async Task<OrderState> ReloadOrderAsync(Guid id)
    {
        OrderState? state = null;

        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders
                .IgnoreQueryFilters()
                .First(x => x.Id == id);

            state = new OrderState(
                order.Id,
                order.Amount,
                order.Currency,
                order.Status,
                order.RazorpayOrderId,
                order.CreatedAt,
                order.UpdatedAt);
        });

        return state!;
    }

    private async Task<int> PaymentOrderCountAsync()
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
            count = db.PaymentOrders
                .IgnoreQueryFilters()
                .Count());

        return count;
    }

    private async Task<List<string>> AuditActionsAsync(
        Guid entityId)
    {
        var actions = new List<string>();

        await WithScopeDbContextAsync(db =>
            actions.AddRange(
                db.AuditLogs
                    .IgnoreQueryFilters()
                    .Where(x => x.EntityId == entityId)
                    .Select(x => x.Action)
                    .ToList()));

        return actions;
    }

    // ==================================================================
    // 1. Retainer invoices
    // ==================================================================

    [Fact(DisplayName =
        "CR1: Client lists only its own retainer invoices")]
    public async Task RetainerInvoice_List_OwnOnly()
    {
        var response = await ClientGet("/api/v1/client/retainer-invoices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        var ids = IdsOf(items!);

        Assert.Contains(TestDataSeeder.Ids.RetainerInvoiceA, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.RetainerInvoiceB, ids);
        Assert.All(items!, x =>
            Assert.NotEqual(
                "INV-R-B-001",
                (string)x!["invoiceNumber"]!));
    }

    [Fact(DisplayName =
        "CR2: Client retainer-invoice list status filter works")]
    public async Task RetainerInvoice_List_StatusFilter()
    {
        var response = await ClientGet(
            "/api/v1/client/retainer-invoices?status=Pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        Assert.Single(items!);
        Assert.Equal(
            TestDataSeeder.Ids.RetainerInvoiceA,
            Guid.Parse((string)items![0]!["id"]!));
        Assert.Equal("Pending", (string)items[0]!["status"]!);
    }

    [Fact(DisplayName =
        "CR3: Client reads its own retainer invoice by ID (200)")]
    public async Task RetainerInvoice_ById_Own_Ok()
    {
        var response = await ClientGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        Assert.Equal(
            "INV-R-A-001",
            (string)body["data"]!["invoiceNumber"]!);
        Assert.Equal(
            TestDataSeeder.Ids.RetainerInvoiceA.ToString(),
            body["data"]!["id"]!.ToString());
    }

    [Fact(DisplayName =
        "CR4: Client reading another company's retainer invoice is 404")]
    public async Task RetainerInvoice_ById_CrossCompany_NotFound()
    {
        var response = await ClientGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceB}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", await ErrorCodeAsync(response));
    }

    [Fact(DisplayName =
        "CR5: Client downloads its own retainer invoice PDF (200)")]
    public async Task RetainerInvoice_Pdf_Own_Ok()
    {
        var response = await ClientGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
    }

    [Fact(DisplayName =
        "CR6: Client downloading another company's retainer invoice PDF is 404")]
    public async Task RetainerInvoice_Pdf_CrossCompany_NotFound()
    {
        var response = await ClientGet(
            $"/api/v1/client/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceB}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ==================================================================
    // 2. Service-fee invoices
    // ==================================================================

    [Fact(DisplayName =
        "SF6: Client lists only its own service-fee invoices")]
    public async Task ServiceFeeInvoice_List_OwnOnly()
    {
        var response = await ClientGet("/api/v1/client/service-fee-invoices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        var ids = IdsOf(items!);

        Assert.Contains(TestDataSeeder.Ids.ServiceFeeInvoiceA, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.ServiceFeeInvoiceB, ids);
    }

    [Fact(DisplayName =
        "SF7: Client service-fee-invoice list status filter works")]
    public async Task ServiceFeeInvoice_List_StatusFilter()
    {
        var response = await ClientGet(
            "/api/v1/client/service-fee-invoices?status=Pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        Assert.Single(items!);
        Assert.Equal(
            TestDataSeeder.Ids.ServiceFeeInvoiceA,
            Guid.Parse((string)items![0]!["id"]!));
    }

    [Fact(DisplayName =
        "SF8: Client reads its own service-fee invoice by ID (200)")]
    public async Task ServiceFeeInvoice_ById_Own_Ok()
    {
        var response = await ClientGet(
            $"/api/v1/client/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceA}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        Assert.Equal(
            "INV-SF-A-001",
            (string)body["data"]!["invoiceNumber"]!);
    }

    [Fact(DisplayName =
        "SF9: Client reading another company's service-fee invoice is 404")]
    public async Task ServiceFeeInvoice_ById_CrossCompany_NotFound()
    {
        var response = await ClientGet(
            $"/api/v1/client/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceB}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "SF10: Client downloads its own service-fee invoice PDF (200)")]
    public async Task ServiceFeeInvoice_Pdf_Own_Ok()
    {
        var response = await ClientGet(
            $"/api/v1/client/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceA}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
    }

    [Fact(DisplayName =
        "SF11: Client downloading another company's service-fee invoice PDF is 404")]
    public async Task ServiceFeeInvoice_Pdf_CrossCompany_NotFound()
    {
        var response = await ClientGet(
            $"/api/v1/client/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceB}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ==================================================================
    // 3. Gov-fee disbursements
    // ==================================================================

    [Fact(DisplayName =
        "GV1: Client lists only its own gov-fee disbursements")]
    public async Task GovFee_List_OwnOnly()
    {
        var response = await ClientGet("/api/v1/client/gov-fee-disbursements");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        var ids = IdsOf(items!);

        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementA, ids);
        Assert.Contains(TestDataSeeder.Ids.GovFeeDisbursementC, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.GovFeeDisbursementB, ids);
        Assert.All(items!, x => Assert.Equal(2, ids.Count));
    }

    [Fact(DisplayName =
        "GV2: Client gov-fee list status filter returns only matching status")]
    public async Task GovFee_List_StatusFilter()
    {
        var response = await ClientGet(
            "/api/v1/client/gov-fee-disbursements?status=PaidByFirm");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        var item = Assert.Single(items!);
        Assert.Equal(
            TestDataSeeder.Ids.GovFeeDisbursementA,
            Guid.Parse((string)item!["id"]!));
        Assert.Equal("PaidByFirm", (string)item["status"]!);
    }

    // ==================================================================
    // 4. Payments - create-order
    // ==================================================================

    [Fact(DisplayName =
        "PO1: Client creates a retainer payment order (200, Razorpay details)")]
    public async Task CreateOrder_Retainer_OwnPending_Ok()
    {
        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        var data = body["data"]!;

        Assert.Equal("Pending", (string)data["status"]!);
        Assert.Equal(FakeRazorpayService.FakeOrderId,
            (string)data["razorpayOrderId"]!);
        Assert.Equal(FakeRazorpayService.FakeKeyId,
            (string)data["razorpayKeyId"]!);
        Assert.Equal(2500m * 100, (int)data["razorpayAmount"]!);
        Assert.Equal(
            TestDataSeeder.Ids.RetainerInvoiceA.ToString(),
            data["retainerInvoiceId"]!.ToString());
    }

    [Fact(DisplayName =
        "PO2: Client creates a service-fee payment order (200)")]
    public async Task CreateOrder_ServiceFee_OwnPending_Ok()
    {
        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "ServiceFee",
                invoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        var data = body["data"]!;

        Assert.Equal("Pending", (string)data["status"]!);
        Assert.Equal(FakeRazorpayService.FakeOrderId,
            (string)data["razorpayOrderId"]!);
        Assert.Equal(
            TestDataSeeder.Ids.ServiceFeeInvoiceA.ToString(),
            data["serviceFeeInvoiceId"]!.ToString());
    }

    [Fact(DisplayName =
        "PO3: Client cannot create an order for another company's invoice (403)")]
    public async Task CreateOrder_CrossCompany_Forbidden()
    {
        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceB
            });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", await ErrorCodeAsync(response));
    }

    [Fact(DisplayName =
        "PO4: Client cannot create an order for a non-pending invoice (409)")]
    public async Task CreateOrder_NonPendingInvoice_Conflict()
    {
        await WithScopeDbContextAsync(db =>
        {
            var invoice = db.RetainerInvoices.First(x =>
                x.Id == TestDataSeeder.Ids.RetainerInvoiceA);
            invoice.Status = RetainerInvoiceStatus.Paid;
            invoice.PaidAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CONFLICT", await ErrorCodeAsync(response));
    }

    [Fact(DisplayName =
        "PO5: Existing Created/Pending order with a Razorpay id is reused (200, no second gateway order)")]
    public async Task CreateOrder_ExistingActiveOrder_Reused_Ok()
    {
        // The seeded order deliberately carries an amount and currency that
        // differ from the invoice, so the assertions below prove the response
        // is projected from the PERSISTED ORDER and never from the invoice.
        var seeded = await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Pending);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await DataNodeAsync(response);

        Assert.Equal(
            seeded.Id.ToString(), data["id"]!.ToString());
        Assert.Equal(
            seeded.RazorpayOrderId,
            (string)data["razorpayOrderId"]!);
        Assert.Equal(
            FakeRazorpayService.FakeKeyId,
            (string)data["razorpayKeyId"]!);
        Assert.Equal("Pending", (string)data["status"]!);

        // Persisted order values, not the invoice's 2500 AED.
        Assert.Equal(
            3000m, decimal.Parse(data["amount"]!.ToString()!));
        Assert.Equal("AED", (string)data["currency"]!);
        Assert.Equal(300000, (int)data["razorpayAmount"]!);

        // No second gateway order, and no second PaymentOrder row.
        Assert.Equal(0, FakeRazorpayService.CreateOrderCallCount);
        Assert.Equal(1, await PaymentOrderCountAsync());
    }

    [Fact(DisplayName =
        "G4-A: Service-fee order in Created with a Razorpay id is reused")]
    public async Task CreateOrder_ServiceFee_CreatedOrder_Reused_Ok()
    {
        var seeded = await SeedOrderAsync(
            serviceFeeInvoiceId: TestDataSeeder.Ids.ServiceFeeInvoiceA,
            status: PaymentStatus.Created);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "ServiceFee",
                invoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await DataNodeAsync(response);

        Assert.Equal(
            seeded.Id.ToString(), data["id"]!.ToString());
        Assert.Equal(
            seeded.RazorpayOrderId,
            (string)data["razorpayOrderId"]!);
        Assert.Equal("Created", (string)data["status"]!);
        Assert.Equal(0, FakeRazorpayService.CreateOrderCallCount);
        Assert.Equal(1, await PaymentOrderCountAsync());
    }

    [Fact(DisplayName =
        "G4-B: Reuse does not mutate the PaymentOrder")]
    public async Task CreateOrder_Reuse_DoesNotMutateOrder()
    {
        var seeded = await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Pending);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = await ReloadOrderAsync(seeded.Id);

        Assert.Equal(
            seeded.CreatedAt, reloaded.CreatedAt);
        Assert.Equal(
            seeded.UpdatedAt, reloaded.UpdatedAt);
        Assert.Equal(seeded.Status, reloaded.Status);
        Assert.Equal(
            seeded.RazorpayOrderId, reloaded.RazorpayOrderId);
        Assert.Equal(seeded.Amount, reloaded.Amount);
        Assert.Equal(seeded.Currency, reloaded.Currency);
    }

    [Fact(DisplayName =
        "G4-C: Reuse writes exactly one PaymentOrderReused audit and no retry/created audit")]
    public async Task CreateOrder_Reuse_WritesReusedAuditOnly()
    {
        var seeded = await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actions = await AuditActionsAsync(seeded.Id);

        Assert.Single(actions);
        Assert.Equal("PaymentOrderReused", actions[0]);
        Assert.DoesNotContain(
            "PaymentOrderCreated", actions);
        Assert.DoesNotContain(
            "PaymentOrderRetried", actions);
    }

    [Fact(DisplayName =
        "G4-D: Cancelled order is terminal and is not reused (409)")]
    public async Task CreateOrder_CancelledOrder_Conflict()
    {
        await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Cancelled);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CONFLICT", await ErrorCodeAsync(response));
        Assert.Equal(0, FakeRazorpayService.CreateOrderCallCount);
    }

    [Fact(DisplayName =
        "G4-E: Paid order is terminal and is not reused (409)")]
    public async Task CreateOrder_PaidOrder_Conflict()
    {
        await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Paid);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, FakeRazorpayService.CreateOrderCallCount);
    }

    [Fact(DisplayName =
        "G4-F: Existing order with a null Razorpay id takes the retry path")]
    public async Task CreateOrder_NullRazorpayOrderId_Retries()
    {
        var seeded = await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Created,
            razorpayOrderId: null);

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await DataNodeAsync(response);

        Assert.Equal(
            seeded.Id.ToString(), data["id"]!.ToString());
        Assert.Equal("Pending", (string)data["status"]!);
        Assert.NotNull((string?)data["razorpayOrderId"]);

        // Exactly one gateway order was created.
        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);

        var reloaded = await ReloadOrderAsync(seeded.Id);

        Assert.Equal(PaymentStatus.Pending, reloaded.Status);
        Assert.NotNull(reloaded.RazorpayOrderId);

        var actions = await AuditActionsAsync(seeded.Id);

        Assert.Contains("PaymentOrderRetried", actions);
        Assert.DoesNotContain(
            "PaymentOrderReused", actions);
    }

    /// <summary>
    /// The seeded-order retry above can only ever see a lone
    /// PaymentOrderRetried, because the seeder writes no creation audit. This
    /// drives the real sequence instead - create the order through the API,
    /// lose the gateway id, then ask again - so the audit trail is read off a
    /// genuine lifecycle. The order is the same row throughout, so both audits
    /// hang off one EntityId and the ordering is meaningful.
    /// </summary>
    [Fact(DisplayName =
        "G4-H: a created order that later retries carries PaymentOrderCreated then PaymentOrderRetried")]
    public async Task CreateOrder_ThenRetry_RecordsBothAuditsInOrder()
    {
        // 1. Create the order for real.
        var created = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var createdData = await DataNodeAsync(created);
        var orderId = Guid.Parse(createdData["id"]!.ToString());
        var firstGatewayOrderId = (string)createdData["razorpayOrderId"]!;

        Assert.False(
            string.IsNullOrWhiteSpace(firstGatewayOrderId));

        Assert.Equal(
            new[] { "PaymentOrderCreated" },
            await AuditActionsAsync(orderId));

        // 2. The gateway id is lost, which is the condition the retry path
        //    exists to recover from.
        await WithScopeDbContextAsync(db =>
        {
            var order = db.PaymentOrders.Single(x => x.Id == orderId);
            order.RazorpayOrderId = null;
            db.SaveChanges();
        });

        // 3. The client asks again and gets the same order back.
        var retried = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);

        var retriedData = await DataNodeAsync(retried);

        // Same order, a new gateway order id.
        Assert.Equal(
            orderId.ToString(), retriedData["id"]!.ToString());
        Assert.NotEqual(
            firstGatewayOrderId,
            (string?)retriedData["razorpayOrderId"]);

        // 4. The lifecycle reads Created then Retried, exactly once each and
        //    in that order, with no Reused audit in between.
        Assert.Equal(
            new[] { "PaymentOrderCreated", "PaymentOrderRetried" },
            await AuditActionsAsync(orderId));
    }

    [Fact(DisplayName =
        "G4-G: Retry sends the PERSISTED order amount and currency, never the invoice's")]
    public async Task CreateOrder_Retry_UsesPersistedOrderAmountAndCurrency()
    {
        // Invoice is 2500 AED; the stored order is 3000 USD. The retry must
        // use the order's values so the gateway order, the response and the
        // webhook's later validation can never disagree.
        var seeded = await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Created,
            razorpayOrderId: null,
            amount: 3000m,
            currency: "USD");

        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);
        Assert.Equal(
            300000,
            FakeRazorpayService.LastRequestedAmountInSmallestUnit);
        Assert.Equal(
            "USD", FakeRazorpayService.LastRequestedCurrency);

        var data = await DataNodeAsync(response);

        // The response still reflects the persisted order.
        Assert.Equal(
            3000m, decimal.Parse(data["amount"]!.ToString()!));
        Assert.Equal("USD", (string)data["currency"]!);
        Assert.Equal(300000, (int)data["razorpayAmount"]!);
    }

    [Fact(DisplayName =
        "G4-H: Concurrent creates for one invoice never yield two gateway orders")]
    public async Task CreateOrder_Concurrent_SingleGatewayOrder()
    {
        var request = () => ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        var responses = await Task.WhenAll(
            request(), request(), request());

        // The unique index keeps exactly one row regardless of outcome.
        Assert.Equal(1, await PaymentOrderCountAsync());

        // Exactly one gateway order exists for the invoice.
        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);

        // Every request resolved cleanly - no 500 from a lost insert race.
        Assert.All(
            responses,
            r => Assert.True(
                r.StatusCode is HttpStatusCode.OK
                    or HttpStatusCode.Conflict,
                $"unexpected {(int)r.StatusCode}"));
    }

    [Fact(DisplayName =
        "G4-I: Concurrent null-id retries create exactly one gateway order")]
    public async Task CreateOrder_Concurrent_NullIdRetry_SingleGatewayOrder()
    {
        var seeded = await SeedOrderAsync(
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            status: PaymentStatus.Created,
            razorpayOrderId: null);

        var request = () => ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });

        var responses = await Task.WhenAll(
            request(), request(), request());

        // The race must collapse to a single gateway order, with no orphan.
        Assert.Equal(1, FakeRazorpayService.CreateOrderCallCount);

        var reloaded = await ReloadOrderAsync(seeded.Id);

        Assert.NotNull(reloaded.RazorpayOrderId);
        Assert.Equal(PaymentStatus.Pending, reloaded.Status);
        Assert.Equal(1, await PaymentOrderCountAsync());

        // Every request resolved to the SAME order - the losers reused it
        // rather than opening a second gateway order.
        Assert.All(
            responses,
            r => Assert.Equal(
                HttpStatusCode.OK, r.StatusCode));

        foreach (var response in responses)
        {
            var data = await DataNodeAsync(response);

            Assert.Equal(
                seeded.Id.ToString(), data["id"]!.ToString());
            Assert.Equal(
                reloaded.RazorpayOrderId,
                (string)data["razorpayOrderId"]!);
        }
    }

    [Fact(DisplayName =
        "PO6: Unknown invoiceType is rejected with 400")]
    public async Task CreateOrder_UnknownType_BadRequest()
    {
        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Bogus",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "PO7: Missing invoice type is rejected with 400")]
    public async Task CreateOrder_MissingType_BadRequest()
    {
        var response = await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================================================================
    // 5. Payments - list
    // ==================================================================

    [Fact(DisplayName =
        "PO8: Client sees only its own payment orders")]
    public async Task Payments_List_OwnOnly()
    {
        await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            });
        await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "ServiceFee",
                invoiceId = TestDataSeeder.Ids.ServiceFeeInvoiceA
            });
        await ClientPost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceB
            });

        var response = await ClientGet("/api/v1/client/payments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await DataItemsAsync(response);
        Assert.Equal(2, items!.Count);
        Assert.All(items!, x =>
        {
            var companyId = Guid.Parse((string)x!["clientCompanyId"]!);
            Assert.Equal(CompanyA, companyId);
        });
    }

    // ==================================================================
    // 6. Service report
    // ==================================================================

    [Fact(DisplayName =
        "SR1: Client downloads its own service report PDF (200)")]
    public async Task ServiceReport_Pdf_Own_Ok()
    {
        var response = await ClientGet(
            "/api/v1/client/service-report?from=2025-01-01&to=2025-12-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
    }

    [Fact(DisplayName =
        "SR2: Invalid date range returns 400")]
    public async Task ServiceReport_InvalidRange_BadRequest()
    {
        var response = await ClientGet(
            "/api/v1/client/service-report?from=2025-12-31&to=2025-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ErrorCodeAsync(response));
    }

    [Fact(DisplayName =
        "SR3: Date range exceeding 365 days returns 400")]
    public async Task ServiceReport_TooLongRange_BadRequest()
    {
        var response = await ClientGet(
            "/api/v1/client/service-report?from=2024-01-01&to=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================================================================
    // 7. Role / security gates
    // ==================================================================

    [Fact(DisplayName =
        "SE1: Anonymous access to client billing endpoints is 401")]
    public async Task Anonymous_Unauthorized()
    {
        var response = await AuthClient.GetAsync(
            "/api/v1/client/retainer-invoices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "SE2: Agent role is rejected from client billing endpoints (403)")]
    public async Task Agent_Forbidden()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/retainer-invoices", AgentA, "Agent"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "SE3: Client with no company resolved gets 404 on list endpoint")]
    public async Task Client_NoCompany_NotFound()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                "/api/v1/client/retainer-invoices",
                ClientUserA,
                "Client",
                clientCompanyId: null));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "CLIENT_COMPANY_NOT_FOUND",
            await ErrorCodeAsync(response));
    }
}
