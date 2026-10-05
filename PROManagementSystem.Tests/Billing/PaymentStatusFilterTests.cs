using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// D3: regression coverage for the <c>status</c> query filter on the payment
/// order lists.
///
/// The handler used to forward <c>request.Status?.ToString()</c> ("Paid",
/// "Pending", ...) into a repository that compares the value against the
/// integer <c>payment_orders."Status"</c> column. PostgreSQL has no
/// <c>integer = text</c> operator, so every filtered request failed with a
/// type error and surfaced as HTTP 500. These tests pin the corrected
/// behaviour: the enum is bound as an integer and the filter actually
/// narrows the result set.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PaymentStatusFilterTests : IntegrationTestBase
{
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;

    public PaymentStatusFilterTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private Task<HttpResponseMessage> ClientGet(string url)
        => ScopeClient.SendAsync(
            ScopeGet(
                url,
                ClientUserA,
                "Client",
                CompanyA.ToString()));

    private Task<HttpResponseMessage> AdminGet(string url)
        => ScopeClient.SendAsync(
            ScopeGet(url, Admin, "Admin"));

    private static async Task<JsonArray> ItemsAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        return body["data"]!["items"]!.AsArray();
    }

    private static async Task<int> TotalCountAsync(
        HttpResponseMessage response)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        return (int)body["data"]!["totalCount"]!;
    }

    /// <summary>
    /// <c>payment_orders</c> has a unique filtered index on both
    /// <c>RetainerInvoiceId</c> and <c>ServiceFeeInvoiceId</c>, so every order
    /// must be attached to a distinct seeded invoice.
    /// </summary>
    private Task SeedOrderAsync(
        PaymentStatus status,
        Guid? retainerInvoiceId,
        Guid? serviceFeeInvoiceId,
        string? razorpayOrderId)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.PaymentOrders.Add(new PaymentOrder
            {
                Id = Guid.NewGuid(),
                InvoiceType = retainerInvoiceId.HasValue
                    ? PaymentInvoiceType.RetainerInvoice
                    : PaymentInvoiceType.ServiceFeeInvoice,
                RetainerInvoiceId = retainerInvoiceId,
                ServiceFeeInvoiceId = serviceFeeInvoiceId,
                ClientCompanyId = CompanyA,
                Amount = 1000m,
                Currency = "AED",
                Status = status,
                RazorpayOrderId = razorpayOrderId,
                RazorpayPaymentId = status == PaymentStatus.Paid
                    ? "pay_filter_paid"
                    : null,
                RazorpaySignature = null,
                PaidAt = status == PaymentStatus.Paid ? now : null,
                FailedAt = null,
                CancelledAt = null,
                Notes = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

    /// <summary>
    /// One order per status, each on a distinct invoice, all owned by
    /// CompanyA so both the client-scoped and the admin list can see them.
    /// </summary>
    private async Task SeedOneOrderPerStatusAsync()
    {
        await SeedOrderAsync(
            PaymentStatus.Created,
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceA,
            serviceFeeInvoiceId: null,
            razorpayOrderId: "rzp_filter_created");

        await SeedOrderAsync(
            PaymentStatus.Pending,
            retainerInvoiceId: null,
            serviceFeeInvoiceId: TestDataSeeder.Ids.ServiceFeeInvoiceA,
            razorpayOrderId: "rzp_filter_pending");

        await SeedOrderAsync(
            PaymentStatus.Paid,
            retainerInvoiceId: TestDataSeeder.Ids.RetainerInvoiceB,
            serviceFeeInvoiceId: null,
            razorpayOrderId: "rzp_filter_paid");
    }

    private static List<int> StatusesOf(JsonArray items)
        => items
            .Select(x => (int)x!["status"]!)
            .ToList();

    // ==================================================================
    // Client list - GET /api/v1/client/payments?status=
    // ==================================================================

    [Fact(DisplayName =
        "D3-PSF1: client payments ?status=Paid returns 200 with only Paid orders")]
    public async Task Client_StatusPaid_ReturnsOnlyPaid()
    {
        await SeedOneOrderPerStatusAsync();

        var response = await ClientGet(
            "/api/v1/client/payments?status=Paid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Single(items);
        Assert.Equal((int)PaymentStatus.Paid, (int)items[0]!["status"]!);
        Assert.Equal(1, await TotalCountAsync(response));
    }

    [Fact(DisplayName =
        "D3-PSF2: client payments ?status=Pending returns 200 with only Pending orders")]
    public async Task Client_StatusPending_ReturnsOnlyPending()
    {
        await SeedOneOrderPerStatusAsync();

        var response = await ClientGet(
            "/api/v1/client/payments?status=Pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Single(items);
        Assert.Equal(
            (int)PaymentStatus.Pending,
            (int)items[0]!["status"]!);
    }

    [Fact(DisplayName =
        "D3-PSF3: client payments ?status=Created returns 200 with only Created orders")]
    public async Task Client_StatusCreated_ReturnsOnlyCreated()
    {
        await SeedOneOrderPerStatusAsync();

        var response = await ClientGet(
            "/api/v1/client/payments?status=Created");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Single(items);
        Assert.Equal(
            (int)PaymentStatus.Created,
            (int)items[0]!["status"]!);
    }

    [Fact(DisplayName =
        "D3-PSF4: client payments ?status=Refunded returns 200 and an empty page when nothing is refunded")]
    public async Task Client_StatusRefunded_EmptyWhenNoneSeeded()
    {
        await SeedOneOrderPerStatusAsync();

        var response = await ClientGet(
            "/api/v1/client/payments?status=Refunded");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Empty(items);
        Assert.Equal(0, await TotalCountAsync(response));
    }

    [Fact(DisplayName =
        "D3-PSF5: client payments ?status=Refunded returns only the refunded order when one exists")]
    public async Task Client_StatusRefunded_ReturnsOnlyRefunded()
    {
        await SeedOneOrderPerStatusAsync();

        await SeedOrderAsync(
            PaymentStatus.Refunded,
            retainerInvoiceId: null,
            serviceFeeInvoiceId: TestDataSeeder.Ids.ServiceFeeInvoiceB,
            razorpayOrderId: "rzp_filter_refunded");

        var response = await ClientGet(
            "/api/v1/client/payments?status=Refunded");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Single(items);
        Assert.Equal(
            (int)PaymentStatus.Refunded,
            (int)items[0]!["status"]!);
    }

    [Fact(DisplayName =
        "D3-PSF6: client payments without a status filter returns 200 and every own order")]
    public async Task Client_NoStatus_ReturnsAll()
    {
        await SeedOneOrderPerStatusAsync();

        var response = await ClientGet(
            "/api/v1/client/payments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Equal(3, items.Count);
        Assert.Equal(3, await TotalCountAsync(response));

        Assert.Equal(
            new[]
            {
                (int)PaymentStatus.Created,
                (int)PaymentStatus.Pending,
                (int)PaymentStatus.Paid
            }.OrderBy(x => x),
            StatusesOf(items).OrderBy(x => x));
    }

    // ==================================================================
    // Admin list - GET /api/v1/payment-orders?status=
    // ==================================================================

    [Fact(DisplayName =
        "D3-PSF7: admin payment-orders ?status=Paid returns 200 with only Paid orders")]
    public async Task Admin_StatusPaid_ReturnsOnlyPaid()
    {
        await SeedOneOrderPerStatusAsync();

        var response = await AdminGet(
            "/api/v1/payment-orders?status=Paid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync(response);

        Assert.Single(items);
        Assert.Equal((int)PaymentStatus.Paid, (int)items[0]!["status"]!);
    }
}
