using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Pagination;

/// <summary>
/// Verifies the renewal-task list endpoints honour explicit pagination while
/// preserving their existing filters and scope:
///   - GET /api/v1/admin/tasks  (Admin sees every non-deleted task)
///   - GET /api/v1/agent/tasks  (Agent sees only tasks assigned to them)
///   - GET /api/v1/client/tasks (Client sees only their company's tasks)
/// Pages are disjoint, deterministic, stable across repeated calls, and
/// navigate past the last item without error. Out-of-range bounds are
/// rejected with a 400 instead of a 500 (Day-6 H2 style).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RenewalTasksPaginationTests : IntegrationTestBase
{
    public RenewalTasksPaginationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<string> AdminTasksAsync(string query)
        => await ReadBodyAsync(
            await ScopeClient.SendAsync(
                ScopeGet(
                    $"/api/v1/admin/tasks{query}",
                    Admin,
                    role: "Admin")));

    private async Task<string> AgentTasksAsync(string query)
        => await ReadBodyAsync(
            await ScopeClient.SendAsync(
                ScopeGet(
                    $"/api/v1/agent/tasks{query}",
                    AgentA)));

    private async Task<string> ClientTasksAsync(string query)
        => await ReadBodyAsync(
            await ScopeClient.SendAsync(
                ScopeGet(
                    $"/api/v1/client/tasks{query}",
                    ClientA,
                    role: "Client",
                    clientCompanyId: CompanyA.ToString())));

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response)
        => await response.Content.ReadAsStringAsync();

    private static List<Guid> ItemIds(string body)
        => JsonNode.Parse(body)!
            ["data"]!["items"]!
            .AsArray()
            .Select(item => Guid.Parse((string)item!["id"]!))
            .ToList();

    private static int TotalCount(string body)
        => (int)JsonNode.Parse(body)!["data"]!["totalCount"]!;

    private static async Task AssertValidation400Async(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    // ------------------------------------------------------------------
    // Admin: default pagination and totals
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin task list defaults to every non-deleted task (200)")]
    public async Task Admin_Default_ReturnsAllTasks()
    {
        var body = await AdminTasksAsync("");

        Assert.Equal(7, TotalCount(body));
        Assert.Equal(7, ItemIds(body).Count);
    }

    [Fact(DisplayName =
        "Admin task list paginates with disjoint, deterministic pages")]
    public async Task Admin_PageNavigation_IsDisjointAndDeterministic()
    {
        var allBody = await AdminTasksAsync("?page=1&pageSize=7");
        var allIds = ItemIds(allBody);
        Assert.Equal(7, allIds.Count);

        const int pageSize = 3;

        var page1Ids = ItemIds(await AdminTasksAsync(
            $"?page=1&pageSize={pageSize}"));
        var page2Ids = ItemIds(await AdminTasksAsync(
            $"?page=2&pageSize={pageSize}"));
        var page3Ids = ItemIds(await AdminTasksAsync(
            $"?page=3&pageSize={pageSize}"));

        var collected = new HashSet<Guid>(page1Ids);
        collected.UnionWith(page2Ids);
        collected.UnionWith(page3Ids);

        Assert.Equal(7, collected.Count);
        Assert.Equal(allIds.Count, collected.Count);
        Assert.Empty(page1Ids.Distinct().Except(page1Ids));
        Assert.Empty(page1Ids.Intersect(page2Ids));
        Assert.Empty(page1Ids.Intersect(page3Ids));
        Assert.Empty(page2Ids.Intersect(page3Ids));

        // Deterministic ordering: a repeated request returns the same page.
        var page1RepeatIds = ItemIds(await AdminTasksAsync(
            $"?page=1&pageSize={pageSize}"));

        Assert.Equal(page1Ids, page1RepeatIds);
    }

    [Fact(DisplayName =
        "Admin task list page beyond range returns 200 with empty items")]
    public async Task Admin_PageBeyondRange_ReturnsEmptyItems()
    {
        var body = await AdminTasksAsync("?page=99&pageSize=7");

        Assert.Equal(7, TotalCount(body));
        Assert.Empty(ItemIds(body));
    }

    // ------------------------------------------------------------------
    // Agent scope
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent task list defaults to only the agent's tasks (200)")]
    public async Task Agent_Default_ReturnsOwnTasksOnly()
    {
        var body = await AgentTasksAsync("");

        Assert.Equal(4, TotalCount(body));

        var ids = ItemIds(body);
        Assert.Contains(TestDataSeeder.Ids.TaskA, ids);
        Assert.Contains(TestDataSeeder.Ids.TaskC, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.TaskB, ids);
    }

    [Fact(DisplayName =
        "Agent task list paginates within the agent's own scope")]
    public async Task Agent_PageNavigation_StaysScoped()
    {
        var body = await AgentTasksAsync("?pageSize=2");

        Assert.Equal(4, TotalCount(body));
        Assert.Equal(2, ItemIds(body).Count);

        var page2Ids = ItemIds(
            await AgentTasksAsync("?page=2&pageSize=2"));

        Assert.Equal(2, page2Ids.Count);
        Assert.Empty(ItemIds(body).Intersect(page2Ids));
        Assert.DoesNotContain(TestDataSeeder.Ids.TaskB, page2Ids);
    }

    [Fact(DisplayName =
        "Agent task list accepts an empty page beyond the last task")]
    public async Task Agent_PageBeyondRange_ReturnsEmptyItems()
    {
        var body = await AgentTasksAsync("?page=50&pageSize=4");

        Assert.Equal(4, TotalCount(body));
        Assert.Empty(ItemIds(body));
    }

    // ------------------------------------------------------------------
    // Client scope
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Client task list defaults to only the client company's tasks (200)")]
    public async Task Client_Default_ReturnsCompanyTasksOnly()
    {
        var body = await ClientTasksAsync("");

        Assert.Equal(2, TotalCount(body));

        var ids = ItemIds(body);
        Assert.Contains(TestDataSeeder.Ids.TaskA, ids);
        Assert.Contains(TestDataSeeder.Ids.TaskC, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.TaskB, ids);
    }

    [Fact(DisplayName =
        "Client task list paginates within the client company's scope")]
    public async Task Client_PageNavigation_StaysScoped()
    {
        var body = await ClientTasksAsync("?pageSize=1");

        Assert.Equal(2, TotalCount(body));
        Assert.Single(ItemIds(body));

        var page2Ids = ItemIds(
            await ClientTasksAsync("?page=2&pageSize=1"));

        Assert.Single(page2Ids);
        Assert.Empty(ItemIds(body).Intersect(page2Ids));
        Assert.DoesNotContain(TestDataSeeder.Ids.TaskB, page2Ids);
    }

    [Fact(DisplayName =
        "Client task list accepts an empty page beyond the last task")]
    public async Task Client_PageBeyondRange_ReturnsEmptyItems()
    {
        var body = await ClientTasksAsync("?page=50&pageSize=2");

        Assert.Equal(2, TotalCount(body));
        Assert.Empty(ItemIds(body));
    }

    // ------------------------------------------------------------------
    // Validation bounds (400, never 500)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("/api/v1/admin/tasks?page=0")]
    [InlineData("/api/v1/admin/tasks?pageSize=0")]
    [InlineData("/api/v1/admin/tasks?pageSize=1001")]
    [InlineData("/api/v1/agent/tasks?page=0")]
    [InlineData("/api/v1/agent/tasks?pageSize=0")]
    [InlineData("/api/v1/agent/tasks?pageSize=1001")]
    [InlineData("/api/v1/client/tasks?page=0")]
    [InlineData("/api/v1/client/tasks?pageSize=0")]
    [InlineData("/api/v1/client/tasks?pageSize=1001")]
    public async Task TaskLists_OutOfRangePagination_Returns400(
        string urlAndQuery)
    {
        var role =
            urlAndQuery.StartsWith("/api/v1/admin", StringComparison.Ordinal)
                ? "Admin"
                : urlAndQuery.StartsWith("/api/v1/agent", StringComparison.Ordinal)
                    ? "Agent"
                    : "Client";

        var userId =
            role == "Admin" ? Admin
            : role == "Agent" ? AgentA
            : ClientA;

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                urlAndQuery,
                userId,
                role: role,
                clientCompanyId:
                    role == "Client" ? CompanyA.ToString() : null));

        await AssertValidation400Async(response);
    }
}