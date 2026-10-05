using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Infrastructure.Persistence.Seeders;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Client;

/// <summary>
/// Verifies the Client-scoped notifications surface: user-scoped list,
/// unread-count, ownership-enforcing mark-as-read, and the automatic client
/// notifications produced by ServiceRequestSubmitted / RenewalTaskCompleted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientNotificationsTests : IntegrationTestBase
{
    private static readonly Guid ClientUserA =
        TestDataSeeder.Ids.ClientUserA;

    private static readonly Guid ClientUserB =
        TestDataSeeder.Ids.ClientUserB;

    private static readonly Guid CompanyA =
        TestDataSeeder.Ids.CompanyA;

    private static readonly Guid CompanyF = MockClientCompanySeeder.Ids.Company;

    private static readonly Guid FaamilUser = MockClientCompanySeeder.Ids.Client;

    public ClientNotificationsTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<HttpResponseMessage> ClientGet(
        string url,
        Guid userId,
        Guid? clientCompanyId = null)
        => await ScopeClient.SendAsync(
            ScopeGet(url, userId, "Client",
                clientCompanyId?.ToString()));

    private async Task<HttpResponseMessage> ClientPost(
        string url,
        object? body,
        Guid userId,
        Guid? clientCompanyId)
        => await ScopeClient.SendAsync(
            ScopePost(url, body, userId, "Client",
                clientCompanyId?.ToString()));

    private async Task<int> UnreadCountAsync(
        Guid userId)
    {
        var response = await ClientGet(
            "/api/v1/client/notifications/unread-count", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        return body["data"]!.GetValue<int>();
    }

    private static async Task<bool> ListContainsTitleAsync(
        HttpResponseMessage response,
        string title,
        string excludeTitle)
    {
        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        var items = body["data"]!["items"]!.AsArray();

        var hasTitle = items.Any(item =>
            (string)item!["title"]! == title);
        var hasExclude = items.Any(item =>
            (string)item!["title"]! == excludeTitle);

        return hasTitle && !hasExclude;
    }

    private static void AddNotification(
        ApplicationDbContext db,
        Guid id,
        Guid userId,
        string title)
        => db.Notifications.Add(new Notification
        {
            Id = id,
            UserId = userId,
            Title = title,
            Message = $"Message for {title}",
            IsRead = false,
            ReadAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        });

    // ------------------------------------------------------------------
    // List scoping
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Client with no notifications sees an empty list and zero unread")]
    public async Task Empty_Notifications_and_UnreadZero()
    {
        var response = await ClientGet(
            "/api/v1/client/notifications", ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);
        Assert.Equal(0, await UnreadCountAsync(ClientUserA));
        Assert.Equal(0, await UnreadCountAsync(ClientUserB));
    }

    [Fact(DisplayName =
        "Client notification list shows only their own notifications")]
    public async Task NotificationList_IsScopedToClient()
    {
        await WithScopeDbContextAsync(db =>
        {
            AddNotification(db,
                Guid.Parse("00000000-0000-0000-0000-000000002001"),
                ClientUserA, "Client Inbox A");
            AddNotification(db,
                Guid.Parse("00000000-0000-0000-0000-000000002002"),
                ClientUserB, "Client Inbox B");
            db.SaveChanges();
        });

        var response = await ClientGet(
            "/api/v1/client/notifications", ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;
        var items = body["data"]!["items"]!.AsArray();

        Assert.Contains(items, item =>
            (string)item!["title"]! == "Client Inbox A");
        Assert.DoesNotContain(items, item =>
            (string)item!["title"]! == "Client Inbox B");
    }

    // ------------------------------------------------------------------
    // Unread count + ownership
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Unread-count counts only unread rows for the current client")]
    public async Task UnreadCount_CountsOwnUnreadOnly()
    {
        await WithScopeDbContextAsync(db =>
        {
            AddNotification(db,
                Guid.Parse("00000000-0000-0000-0000-000000002101"),
                ClientUserA, "Unread A1");
            AddNotification(db,
                Guid.Parse("00000000-0000-0000-0000-000000002102"),
                ClientUserA, "Unread A2");
            db.SaveChanges();
        });

        Assert.Equal(2, await UnreadCountAsync(ClientUserA));
        Assert.Equal(0, await UnreadCountAsync(ClientUserB));
    }

    [Fact(DisplayName =
        "Client can mark their own notification as read and unread-count drops")]
    public async Task MarkOwnRead_Returns200_AndUnreadDecrements()
    {
        var notificationId =
            Guid.Parse("00000000-0000-0000-0000-000000002201");

        await WithScopeDbContextAsync(db =>
        {
            AddNotification(db, notificationId, ClientUserA, "Read Me A");
            db.SaveChanges();
        });

        Assert.Equal(1, await UnreadCountAsync(ClientUserA));

        var patch = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/client/notifications/{notificationId}/read",
                new { isRead = true },
                ClientUserA,
                "Client"));

        await EnsureOk(patch, HttpStatusCode.OK);
        Assert.Equal(0, await UnreadCountAsync(ClientUserA));
    }

    [Fact(DisplayName =
        "Client cannot mark another client's notification as read (404)")]
    public async Task MarkOtherClientsNotificationRead_Returns404()
    {
        var notificationId =
            Guid.Parse("00000000-0000-0000-0000-000000002301");

        await WithScopeDbContextAsync(db =>
        {
            AddNotification(db, notificationId, ClientUserA, "Owned By A");
            db.SaveChanges();
        });

        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/client/notifications/{notificationId}/read",
                new { isRead = true },
                ClientUserB,
                "Client"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Automatic client notifications from domain events
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Submitting a service request creates a client notification and bumps unread-count")]
    public async Task ServiceRequestSubmission_CreatesClientNotification()
    {
        var response = await ClientPost(
            "/api/v1/client/service-requests",
            new
            {
                type = "Other",
                employeeId = MockClientCompanySeeder.Ids.EmployeeAhmed.ToString(),
                notes = "Day 5 audit notification test."
            },
            FaamilUser,
            CompanyF);

        await EnsureOk(response, HttpStatusCode.Created);

        var list = await ClientGet(
            "/api/v1/client/notifications", FaamilUser, CompanyF);

        await EnsureOk(list, HttpStatusCode.OK);

        var body = JsonNode.Parse(
            await list.Content.ReadAsStringAsync())!;
        var items = body["data"]!["items"]!.AsArray();

        Assert.Contains(items, item =>
            (string)item!["title"]! == "Service request submitted");

        Assert.Equal(1, await UnreadCountAsync(FaamilUser));
    }

    [Fact(DisplayName =
        "Completing a renewal task creates a client notification for the task's company")]
    public async Task TaskCompletion_CreatesClientNotification()
    {
        var taskId = TestDataSeeder.Ids.TaskA;

        foreach (var status in new[]
        {
            "FeePaid", "AwaitingApproval", "Approved", "Updated"
        })
        {
            // Completing a task requires the renewed document expiry, so only
            // the final step carries one.
            object payload = new { status };

            if (status == "Updated")
            {
                payload = new
                {
                    status,
                    renewedExpiryDate = DateTime.UtcNow.AddYears(1)
                };
            }

            var patch = await ScopeClient.SendAsync(
                ScopePatch(
                    $"/api/v1/agent/tasks/{taskId}/status",
                    payload,
                    TestDataSeeder.Ids.AgentA));

            if (patch.StatusCode != HttpStatusCode.OK)
            {
                Assert.Fail(
                    $"PATCH {status} -> {(int)patch.StatusCode} " +
                    await patch.Content.ReadAsStringAsync());
            }
        }

        var list = await ClientGet(
            "/api/v1/client/notifications", ClientUserA, CompanyA);

        await EnsureOk(list, HttpStatusCode.OK);

        var body = JsonNode.Parse(
            await list.Content.ReadAsStringAsync())!;
        var items = body["data"]!["items"]!.AsArray();

        Assert.Contains(items, item =>
            (string)item!["title"]! == "Renewal task completed");

        Assert.Equal(1, await UnreadCountAsync(ClientUserA));
    }
}