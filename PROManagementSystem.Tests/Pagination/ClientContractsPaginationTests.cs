using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Pagination;

/// <summary>
/// Verifies GET /api/v1/clients/{id}/contracts supports explicit pagination
/// while keeping the pre-existing response shape (Items only, with page and
/// pageSize passed through the request pipeline). Pages must be disjoint,
/// deterministic, and navigable past the last item without error; out-of-range
/// bounds must yield 400, never 500 (Day-6 H2 convention).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientContractsPaginationTests : IntegrationTestBase
{
    public ClientContractsPaginationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<(HttpStatusCode Status, JsonNode? Data)> GetContractsAsync(
        string query = "")
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/clients/{TestDataSeeder.Ids.CompanyA}/contracts{query}",
                TestDataSeeder.Ids.Admin,
                role: "Admin"));

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

    /// <summary>
    /// Company A is seeded with a single contract. Two more are added here so
    /// pagination over multiple pages can be exercised deterministically.
    /// </summary>
    private async Task AddExtraContractsAsync()
    {
        var now = DateTime.UtcNow;

        await WithScopeDbContextAsync(db =>
        {
            db.ServiceContracts.AddRange(
                Contract(
                    Guid.Parse("00000000-0000-0000-0000-900000000001"),
                    TestDataSeeder.Ids.CompanyA,
                    "CT-A-901",
                    DateOnly.FromDateTime(now.AddDays(-10))),
                Contract(
                    Guid.Parse("00000000-0000-0000-0000-900000000002"),
                    TestDataSeeder.Ids.CompanyA,
                    "CT-A-902",
                    DateOnly.FromDateTime(now.AddDays(-50))));
            db.SaveChanges();
        });
    }

    private static ServiceContract Contract(
        Guid id, Guid companyId, string contractNumber, DateOnly startDate)
        => new()
        {
            Id = id,
            ClientCompanyId = companyId,
            ContractNumber = contractNumber,
            StartDate = startDate,
            EndDate = startDate.AddYears(1),
            Status = ServiceContractStatus.Active,
            RetainerAmount = 1000m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

    // ------------------------------------------------------------------
    // Default + response shape
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Contracts list defaults to a full page of the company's contracts (200)")]
    public async Task Contracts_Default_ReturnsAll()
    {
        var (status, data) = await GetContractsAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Single(ItemIds(data));
        Assert.Contains(
            TestDataSeeder.Ids.ContractA,
            ItemIds(data));

        // Response shape is preserved: Items only, no paging metadata.
        Assert.NotNull(data?["items"]);
        Assert.Null(data?["totalCount"]);
        Assert.Null(data?["page"]);
        Assert.Null(data?["pageSize"]);
    }

    // ------------------------------------------------------------------
    // Pagination navigation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Contracts page navigation is disjoint, ordered, and deterministic")]
    public async Task Contracts_PageNavigation_IsDisjointAndDeterministic()
    {
        await AddExtraContractsAsync();
        const int pageSize = 2;

        var (_, all) = await GetContractsAsync("?page=1&pageSize=100");
        var allIds = ItemIds(all);
        Assert.Equal(3, allIds.Count);

        var page1 = ItemIds((await GetContractsAsync(
            $"?page=1&pageSize={pageSize}")).Data);
        var page2 = ItemIds((await GetContractsAsync(
            $"?page=2&pageSize={pageSize}")).Data);

        var union = page1.Concat(page2).ToHashSet();
        Assert.Equal(3, union.Count);
        Assert.Empty(page1.Intersect(page2));
        Assert.Equal(allIds.ToHashSet(), union);

        // Ordering: StartDate DESC, then Id ASC. Seeded start dates are
        // -10d, -30d (ContractA), -50d, so ContractA lands in the middle page.
        var page1Repeat = ItemIds((await GetContractsAsync(
            $"?page=1&pageSize={pageSize}")).Data);
        Assert.Equal(page1, page1Repeat);
    }

    [Fact(DisplayName =
        "Contracts accepts a page with a single remaining item")]
    public async Task Contracts_PageWithRemainder_ReturnsLastItem()
    {
        await AddExtraContractsAsync();

        var (status, data) = await GetContractsAsync("?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Single(ItemIds(data));
    }

    [Fact(DisplayName =
        "Contracts page beyond range returns 200 with empty items")]
    public async Task Contracts_PageBeyondRange_ReturnsEmptyItems()
    {
        var (status, data) = await GetContractsAsync("?page=99&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(ItemIds(data));
    }

    // ------------------------------------------------------------------
    // Validation bounds (400, never 500)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=1001")]
    public async Task Contracts_OutOfRangePagination_Returns400(string query)
    {
        var (status, _) = await GetContractsAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}