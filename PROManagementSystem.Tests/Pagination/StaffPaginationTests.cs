using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Pagination;

/// <summary>
/// Verifies GET /api/v1/admin/staff honours explicit pagination: default
/// page, deterministic page navigation that can never overlap or repeat,
/// empty pages past the last item, the includeInactive switch, and 400s for
/// out-of-range bounds instead of 500s (Day-6 H2 style).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StaffPaginationTests : IntegrationTestBase
{
    public StaffPaginationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<(HttpStatusCode Status, JsonNode? Data)> GetStaffAsync(
        string query = "",
        bool admin = true)
        => await SendAdminAsync($"/api/v1/admin/staff{query}", admin);

    private async Task<(HttpStatusCode Status, JsonNode? Data)> SendAdminAsync(
        string url,
        bool admin = true)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, TestDataSeeder.Ids.Admin, role: "Admin"));

        var body = await response.Content.ReadAsStringAsync();
        JsonNode? data = null;

        if (response.IsSuccessStatusCode)
        {
            data = JsonNode.Parse(body)?["data"];
        }

        return (response.StatusCode, data);
    }

    private static List<Guid> ItemIds(JsonNode? data)
        => data?["items"]
            ?.AsArray()
            .Select(item => Guid.Parse((string)item!["id"]!))
            .ToList() ?? new List<Guid>();

    private static int TotalCount(JsonNode? data)
        => (int?)data?["totalCount"] ?? -1;

    // ------------------------------------------------------------------
    // Default + filters
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Staff list defaults to active agents only (200)")]
    public async Task Staff_Default_ReturnsActiveAgents()
    {
        var (status, data) = await GetStaffAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(4, TotalCount(data));
        Assert.Equal(4, ItemIds(data).Count);

        var ids = ItemIds(data);
        Assert.Contains(TestDataSeeder.Ids.AgentA, ids);
        Assert.Contains(TestDataSeeder.Ids.AgentB, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.InactiveUser, ids);
    }

    [Fact(DisplayName =
        "Staff list with includeInactive=true returns inactive agents too")]
    public async Task Staff_IncludeInactive_ReturnsAllAgents()
    {
        var (status, data) = await GetStaffAsync("?includeInactive=true");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(5, TotalCount(data));
        Assert.Contains(
            TestDataSeeder.Ids.InactiveUser,
            ItemIds(data));
    }

    // ------------------------------------------------------------------
    // Pagination navigation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Staff pages are disjoint, deterministic, and exhaust the full list")]
    public async Task Staff_PageNavigation_IsDisjointAndDeterministic()
    {
        const int pageSize = 2;

        var (_, allData) = await GetStaffAsync("?page=1&pageSize=100");
        var allIds = ItemIds(allData);
        Assert.Equal(4, allIds.Count);
        Assert.Equal(4, TotalCount(allData));

        var page1Ids = ItemIds(
            (await GetStaffAsync($"?page=1&pageSize={pageSize}")).Data);
        var page2Ids = ItemIds(
            (await GetStaffAsync($"?page=2&pageSize={pageSize}")).Data);

        var union = page1Ids.Concat(page2Ids).ToHashSet();
        Assert.Equal(4, union.Count);
        Assert.Empty(page1Ids.Intersect(page2Ids));
        Assert.Equal(allIds.ToHashSet(), union);

        // Deterministic ordering: repeat request returns identical page.
        var page1Repeat = ItemIds(
            (await GetStaffAsync($"?page=1&pageSize={pageSize}")).Data);
        Assert.Equal(page1Ids, page1Repeat);
    }

    [Fact(DisplayName =
        "Staff page beyond last item returns 200 with empty items")]
    public async Task Staff_PageBeyondRange_ReturnsEmptyItems()
    {
        var (status, data) = await GetStaffAsync("?page=99&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(4, TotalCount(data));
        Assert.Empty(ItemIds(data));
    }

    // ------------------------------------------------------------------
    // Validation bounds (400, never 500)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-3")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=1001")]
    public async Task Staff_OutOfRangePagination_Returns400(string query)
    {
        var (status, _) = await GetStaffAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}