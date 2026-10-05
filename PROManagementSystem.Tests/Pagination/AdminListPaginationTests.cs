using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Pagination;

/// <summary>
/// Verifies the admin read-only list endpoints accept explicit pagination and
/// stay deterministic while paging:
///   - GET /api/v1/admin/clients                         (companies)
///   - GET /api/v1/admin/clients/{id}/entities           (entities)
///   - GET /api/v1/admin/clients/{id}/contacts           (contacts)
///   - GET /api/v1/admin/employees                       (employees)
/// These endpoints already supported paging; this suite locks in disjoint,
/// non-overlapping pages, deterministic ordering, tenant isolation, and the
/// Day-6 H2 convention that out-of-range bounds yield 400, never 500.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AdminListPaginationTests : IntegrationTestBase
{
    public AdminListPaginationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<(HttpStatusCode Status, JsonNode? Data)> GetAsync(
        string url)
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
    // Companies (GET /api/v1/admin/clients)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Companies list defaults to all non-deleted companies (200)")]
    public async Task Companies_Default_ReturnsAll()
    {
        var (status, data) =
            await GetAsync("/api/v1/admin/clients");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, TotalCount(data));

        var ids = ItemIds(data);
        Assert.Contains(TestDataSeeder.Ids.CompanyA, ids);
        Assert.Contains(TestDataSeeder.Ids.CompanyB, ids);
    }

    [Fact(DisplayName =
        "Companies page navigation is disjoint and deterministic")]
    public async Task Companies_PageNavigation_IsDisjointAndDeterministic()
    {
        const int pageSize = 2;

        var (_, all) = await GetAsync($"/api/v1/admin/clients?page=1&pageSize=100");
        var allIds = ItemIds(all);
        Assert.Equal(3, allIds.Count);

        var page1 = ItemIds((await GetAsync(
            $"/api/v1/admin/clients?page=1&pageSize={pageSize}")).Data);
        var page2 = ItemIds((await GetAsync(
            $"/api/v1/admin/clients?page=2&pageSize={pageSize}")).Data);
        var page3 = ItemIds((await GetAsync(
            $"/api/v1/admin/clients?page=3&pageSize={pageSize}")).Data);

        var union = page1.Concat(page2).Concat(page3).ToHashSet();
        Assert.Equal(3, union.Count);
        Assert.Empty(page1.Intersect(page2));
        Assert.Empty(page1.Intersect(page3));
        Assert.Empty(page2.Intersect(page3));
        Assert.Equal(allIds.ToHashSet(), union);

        var page1Repeat = ItemIds((await GetAsync(
            $"/api/v1/admin/clients?page=1&pageSize={pageSize}")).Data);
        Assert.Equal(page1, page1Repeat);
    }

    [Fact(DisplayName =
        "Companies page beyond range returns 200 with empty items")]
    public async Task Companies_PageBeyondRange_ReturnsEmptyItems()
    {
        var (status, data) =
            await GetAsync("/api/v1/admin/clients?page=99&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, TotalCount(data));
        Assert.Empty(ItemIds(data));
    }

    [Theory]
    [InlineData("/api/v1/admin/clients?page=0")]
    [InlineData("/api/v1/admin/clients?pageSize=0")]
    [InlineData("/api/v1/admin/clients?pageSize=1001")]
    public async Task Companies_OutOfRangePagination_Returns400(string url)
    {
        var (status, _) = await GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    // ------------------------------------------------------------------
    // Entities (GET /api/v1/admin/clients/{id}/entities)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Entities list is scoped to the requested client company")]
    public async Task Entities_ForCompanyA_ReturnsOwnEntitiesOnly()
    {
        var (status, data) = await GetAsync(
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/entities");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, TotalCount(data));

        var ids = ItemIds(data);
        Assert.Contains(TestDataSeeder.Ids.EntityA, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.EntityB, ids);
    }

    [Fact(DisplayName =
        "Entities page beyond range returns 200 with empty items")]
    public async Task Entities_PageBeyondRange_ReturnsEmptyItems()
    {
        var (status, data) = await GetAsync(
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/entities" +
            "?page=99&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, TotalCount(data));
        Assert.Empty(ItemIds(data));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=1001")]
    public async Task Entities_OutOfRangePagination_Returns400(string query)
    {
        var (status, _) = await GetAsync(
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/entities" +
            query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    // ------------------------------------------------------------------
    // Contacts (GET /api/v1/admin/clients/{id}/contacts)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Contacts list is scoped to the requested client company")]
    public async Task Contacts_ForCompanyA_ReturnsOwnContactsOnly()
    {
        var (status, data) = await GetAsync(
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/contacts");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, TotalCount(data));

        var ids = ItemIds(data);
        Assert.Contains(TestDataSeeder.Ids.ClientUserA, ids);
        Assert.DoesNotContain(TestDataSeeder.Ids.ClientUserB, ids);
    }

    [Fact(DisplayName =
        "Contacts page beyond range returns 200 with empty items")]
    public async Task Contacts_PageBeyondRange_ReturnsEmptyItems()
    {
        var (status, data) = await GetAsync(
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/contacts" +
            "?page=99&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, TotalCount(data));
        Assert.Empty(ItemIds(data));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=1001")]
    public async Task Contacts_OutOfRangePagination_Returns400(string query)
    {
        var (status, _) = await GetAsync(
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/contacts" +
            query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    // ------------------------------------------------------------------
    // Employees (GET /api/v1/admin/employees)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Employees list defaults to all employees (200)")]
    public async Task Employees_Default_ReturnsAll()
    {
        var (status, data) = await GetAsync("/api/v1/admin/employees");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(4, TotalCount(data));
    }

    [Fact(DisplayName =
        "Employees page navigation is disjoint and deterministic")]
    public async Task Employees_PageNavigation_IsDisjointAndDeterministic()
    {
        const int pageSize = 2;

        var (_, all) = await GetAsync($"/api/v1/admin/employees?page=1&pageSize=100");
        var allIds = ItemIds(all);
        Assert.Equal(4, allIds.Count);

        var page1 = ItemIds((await GetAsync(
            $"/api/v1/admin/employees?page=1&pageSize={pageSize}")).Data);
        var page2 = ItemIds((await GetAsync(
            $"/api/v1/admin/employees?page=2&pageSize={pageSize}")).Data);

        var union = page1.Concat(page2).ToHashSet();
        Assert.Equal(4, union.Count);
        Assert.Empty(page1.Intersect(page2));
        Assert.Equal(allIds.ToHashSet(), union);

        var page1Repeat = ItemIds((await GetAsync(
            $"/api/v1/admin/employees?page=1&pageSize={pageSize}")).Data);
        Assert.Equal(page1, page1Repeat);
    }

    [Theory]
    [InlineData("/api/v1/admin/employees?page=0")]
    [InlineData("/api/v1/admin/employees?pageSize=0")]
    [InlineData("/api/v1/admin/employees?pageSize=1001")]
    public async Task Employees_OutOfRangePagination_Returns400(string url)
    {
        var (status, _) = await GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}