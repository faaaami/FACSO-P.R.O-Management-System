using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Scope;

/// <summary>
/// Verifies strict task-linked scope enforcement: every Agent resource the
/// agent touches must trace back to a renewal_task whose AssignedStaffId is
/// the current agent. Everything owned by another agent's world is 404.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ScopeTests : IntegrationTestBase
{
    public ScopeTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;

    // ------------------------------------------------------------------
    // Tasks
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent can read their own task by id (200)")]
    public async Task OwnTask_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/tasks/{TestDataSeeder.Ids.TaskA}", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "Agent cannot read another agent's task by id (404)")]
    public async Task OtherAgentsTask_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/tasks/{TestDataSeeder.Ids.TaskB}", AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent cannot read a non-existent task by id (404)")]
    public async Task UnknownTask_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/tasks/{TestDataSeeder.Ids.Unknown}", AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent listing tasks returns only their own tasks")]
    public async Task TaskList_IsScopedToAgent()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/tasks", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            TestDataSeeder.Ids.TaskA.ToString(), body);
        Assert.DoesNotContain(
            TestDataSeeder.Ids.TaskB.ToString(), body);
    }

    // ------------------------------------------------------------------
    // Documents
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent document list is strictly task-linked (only own documents)")]
    public async Task DocumentList_IsTaskLinked()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/documents", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("DOC-A-001", body);
        Assert.DoesNotContain("DOC-B-001", body);
    }

    [Fact(DisplayName =
        "Agent expiring-documents list is strictly task-linked")]
    public async Task ExpiringDocumentList_IsTaskLinked()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/documents/expiring", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("DOC-A-001", body);
        Assert.DoesNotContain("DOC-B-001", body);
    }

    [Fact(DisplayName =
        "Agent can read a document in their task-linked scope (200)")]
    public async Task OwnDocument_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "Agent cannot read a document outside their task-linked scope (404)")]
    public async Task OtherAgentsDocument_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentB}", AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent cannot patch a document outside their scope (404)")]
    public async Task PatchOtherAgentsDocument_Returns404()
    {
        var request = ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentB}",
            new { documentNumber = "DOC-B-CHANGED" },
            AgentA);

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent can patch a document in their scope (200)")]
    public async Task PatchOwnDocument_Returns200()
    {
        var request = ScopePatch(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}",
            new { documentNumber = "DOC-A-CHANGED" },
            AgentA);

        var response = await ScopeClient.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "Agent document dependencies are scope-checked (404 for others)")]
    public async Task OtherAgentsDocumentDependencies_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentB}/dependencies",
                AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Clients / Entities / Employees
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent clients list is restricted to task-linked companies")]
    public async Task ClientsList_IsTaskLinked()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/clients", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Company A", body);
        Assert.DoesNotContain("Company B", body);
    }

    [Fact(DisplayName =
        "Agent cannot fetch a client outside their scope (404)")]
    public async Task OtherAgentsClient_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/clients/{TestDataSeeder.Ids.CompanyB}", AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent employees list is restricted to task-linked employees")]
    public async Task EmployeesList_IsTaskLinked()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/employees", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Employee A", body);
        Assert.DoesNotContain("Employee B", body);
    }

    [Fact(DisplayName =
        "Agent cannot fetch another agent's employee (404)")]
    public async Task OtherAgentsEmployee_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/employees/{TestDataSeeder.Ids.EmployeeB}", AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Notifications
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent notification list shows only their own notifications")]
    public async Task NotificationList_IsScopedToAgent()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/notifications", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Inbox A", body);
        Assert.DoesNotContain("Inbox B", body);
    }

    [Fact(DisplayName =
        "Agent can mark their own notification as read (200)")]
    public async Task MarkOwnNotificationRead_Returns200()
    {
        var request = ScopePatch(
            $"/api/v1/agent/notifications/{TestDataSeeder.Ids.NotificationA}/read",
            new { isRead = true },
            AgentA);

        var response = await ScopeClient.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "Agent cannot mark another agent's notification as read (404)")]
    public async Task MarkOtherAgentsNotificationRead_Returns404()
    {
        var request = ScopePatch(
            $"/api/v1/agent/notifications/{TestDataSeeder.Ids.NotificationB}/read",
            new { isRead = true },
            AgentA);

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Workload + account
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent workload endpoint returns 200 for an agent")]
    public async Task Workload_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/agent/workload", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
    }
}
