using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Notifications;

/// <summary>
/// Every role is told about the changes that need them, in plain language,
/// and every role can read its own unread count.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NotificationCoverageTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;

    public NotificationCoverageTests(DatabaseFixture fixture)
        : base(fixture) { }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<List<Notification>> NotificationsForAsync(Guid userId)
    {
        var list = new List<Notification>();

        await WithScopeDbContextAsync(db =>
            list = db.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.CreatedAt)
                .ToList());

        return list;
    }

    private static void AssertReadable(Notification notification)
    {
        // No internal markers or raw ids in what a person reads.
        Assert.DoesNotContain("Id:", notification.Message);
        Assert.DoesNotContain(" | ", notification.Message);
    }

    private async Task BlockAsAgentAsync(string reason)
        => await EnsureOk(
            await ScopeClient.SendAsync(ScopePatch(
                $"/api/v1/agent/tasks/{TaskA}/block",
                new { blockedReason = reason },
                AgentA,
                "Agent")),
            HttpStatusCode.OK);

    private async Task<int> UnreadCountAsync(string url, Guid userId, string role)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, userId, role));

        await EnsureOk(response, HttpStatusCode.OK);

        return JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!["data"]!
            .GetValue<int>();
    }

    // ------------------------------------------------------------------
    // Tasks
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "N-1: an agent blocking a task notifies the admins with the reason")]
    public async Task AgentBlock_NotifiesAdmins()
    {
        await BlockAsAgentAsync("Waiting for the client's passport scan.");

        var notification = Assert.Single(
            await NotificationsForAsync(Admin),
            x => x.Title == "Task blocked");

        Assert.Contains("Waiting for the client's passport scan.", notification.Message);
        AssertReadable(notification);
    }

    [Fact(DisplayName =
        "N-2: an admin blocking a task notifies the assigned agent")]
    public async Task AdminBlock_NotifiesAgent()
    {
        await EnsureOk(
            await ScopeClient.SendAsync(ScopePatch(
                $"/api/v1/admin/tasks/{TaskA}/block",
                new { blockedReason = "On hold until the fee is paid." },
                Admin,
                "Admin")),
            HttpStatusCode.OK);

        var notification = Assert.Single(
            await NotificationsForAsync(AgentA),
            x => x.Title == "Task blocked");

        AssertReadable(notification);
        Assert.DoesNotContain(
            await NotificationsForAsync(Admin),
            x => x.Title == "Task blocked");
    }

    [Fact(DisplayName =
        "N-3: unblocking a task notifies the assigned agent")]
    public async Task Unblock_NotifiesAgent()
    {
        await BlockAsAgentAsync("Waiting for documents.");

        await EnsureOk(
            await ScopeClient.SendAsync(ScopePatch(
                $"/api/v1/admin/tasks/{TaskA}/unblock",
                new { unblockReason = "Documents received." },
                Admin,
                "Admin")),
            HttpStatusCode.OK);

        var notification = Assert.Single(
            await NotificationsForAsync(AgentA),
            x => x.Title == "Task unblocked");

        Assert.Contains("Documents received.", notification.Message);
        AssertReadable(notification);
    }

    // ------------------------------------------------------------------
    // Invoices
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "N-4: a new retainer invoice notifies the client")]
    public async Task NewRetainerInvoice_NotifiesClient()
    {
        var response = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/retainer-invoices",
            new
            {
                clientCompanyId = TestDataSeeder.Ids.CompanyA,
                serviceContractId = TestDataSeeder.Ids.ContractA,
                invoiceNumber = "RI-NOTIFY-1",
                invoiceDate = "2026-11-01",
                dueDate = "2026-11-15",
                periodStart = "2026-11-01",
                periodEnd = "2026-11-30",
                amount = 1250m,
                currency = "AED"
            },
            Admin,
            "Admin"));

        await EnsureOk(response, HttpStatusCode.Created);

        var notification = Assert.Single(
            await NotificationsForAsync(ClientUserA),
            x => x.Title == "New invoice");

        Assert.Contains("RI-NOTIFY-1", notification.Message);
        Assert.Contains("AED 1,250.00", notification.Message);
        Assert.Contains("15 Nov 2026", notification.Message);
    }

    [Fact(DisplayName =
        "N-5: marking an invoice paid notifies the client")]
    public async Task MarkPaid_NotifiesClient()
    {
        await EnsureOk(
            await ScopeClient.SendAsync(ScopePatch(
                $"/api/v1/retainer-invoices/{TestDataSeeder.Ids.RetainerInvoiceA}/mark-paid",
                null,
                Admin,
                "Admin")),
            HttpStatusCode.OK);

        var notification = Assert.Single(
            await NotificationsForAsync(ClientUserA),
            x => x.Title == "Invoice paid");

        Assert.Contains("INV-R-A-001", notification.Message);
    }

    [Fact(DisplayName =
        "N-6: voiding an invoice notifies the client")]
    public async Task Void_NotifiesClient()
    {
        await EnsureOk(
            await ScopeClient.SendAsync(ScopePatch(
                $"/api/v1/service-fee-invoices/{TestDataSeeder.Ids.ServiceFeeInvoiceA}/void",
                new { reason = "Raised in error." },
                Admin,
                "Admin")),
            HttpStatusCode.OK);

        var notification = Assert.Single(
            await NotificationsForAsync(ClientUserA),
            x => x.Title == "Invoice cancelled");

        Assert.Contains("INV-SF-A-001", notification.Message);
    }

    [Fact(DisplayName =
        "N-7: an online payment notifies the client and the admins")]
    public async Task OnlinePayment_NotifiesClientAndAdmins()
    {
        var created = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/client/payments/create-order",
            new
            {
                invoiceType = "Retainer",
                invoiceId = TestDataSeeder.Ids.RetainerInvoiceA
            },
            ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString()));

        await EnsureOk(created, HttpStatusCode.OK);

        var data = JsonNode.Parse(
            await created.Content.ReadAsStringAsync())!["data"]!;
        var gatewayOrderId = (string)data["razorpayOrderId"]!;
        var amount = (int)data["razorpayAmount"]!;
        var currency = (string)data["currency"]!;

        var rawBody =
            "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{" +
            "\"id\":\"pay_notify_1\"," +
            "\"order_id\":\"" + gatewayOrderId + "\"," +
            "\"amount\":" + amount + ",\"currency\":\"" + currency + "\"," +
            "\"status\":\"captured\"}}}}";

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(FakeRazorpayService.FakeWebhookSecret));

        var webhook = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/client/payments/webhook")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add(
            "X-Razorpay-Signature",
            Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
                .ToLowerInvariant());
        webhook.Headers.Add("X-Razorpay-Event-Id", "evt_notify_1");

        await EnsureOk(await ScopeClient.SendAsync(webhook), HttpStatusCode.OK);

        var receipt = Assert.Single(
            await NotificationsForAsync(ClientUserA),
            x => x.Title == "Payment received");
        Assert.Contains("INV-R-A-001", receipt.Message);

        Assert.Single(
            await NotificationsForAsync(Admin),
            x => x.Title == "Online payment received");
    }

    // ------------------------------------------------------------------
    // Unread counts
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "N-8: admins and agents can read their own unread count")]
    public async Task UnreadCount_ForAdminAndAgent()
    {
        Assert.Equal(0, await UnreadCountAsync(
            "/api/v1/notifications/unread-count", Admin, "Admin"));

        // The seed data already gives Agent A one unread notification.
        var agentBefore = await UnreadCountAsync(
            "/api/v1/agent/notifications/unread-count", AgentA, "Agent");

        await BlockAsAgentAsync("Waiting for documents.");

        Assert.Equal(1, await UnreadCountAsync(
            "/api/v1/notifications/unread-count", Admin, "Admin"));

        // The agent's own count is unaffected by the admins' notification.
        Assert.Equal(agentBefore, await UnreadCountAsync(
            "/api/v1/agent/notifications/unread-count", AgentA, "Agent"));
    }
}
