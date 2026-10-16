using System.Net;
using System.Net.Http.Json;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// D1: the pending-extraction review queue.
///
/// Before D1 the only way to reach a draft was the response of the upload that
/// created it, so navigating away lost it. These tests pin the new paged list:
/// Admin sees the whole Admin scope, Agent sees its own drafts plus the legacy
/// NULL-CreatedBy rows that confirmation still accepts, and the terminal
/// states are excluded by default.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PendingDocumentExtractionListingTests
    : IntegrationTestBase
{
    private const string AdminListUrl =
        "/api/v1/admin/documents/extractions";

    private const string AgentListUrl =
        "/api/v1/agent/documents/extractions";

    public PendingDocumentExtractionListingTests(
        DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<Guid> SeedExtractionAsync(
        Guid? createdBy,
        string status = "Pending",
        bool isDeleted = false,
        DocumentType? suggestedType = DocumentType.Passport,
        string? failureCode = null)
    {
        var id = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = id,
                FileName = "upload.pdf",
                FileUrl = $"extractions/{id:N}.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                SuggestedType = suggestedType,
                SuggestedDocumentNumber = "D1-SUG-001",
                SuggestedIssueDate = now.AddYears(-1),
                SuggestedExpiryDate = now.AddYears(4),
                SuggestedOwnerType = "Employee",
                SuggestedDetails = "{}",
                FailureCode = failureCode,
                Status = status,
                ExpiresAt = now.AddHours(10),
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now : null
            });

            db.SaveChanges();
        });

        return id;
    }

    private async Task<ListEnvelope> ListAsync(
        string url,
        Guid userId,
        string role,
        string? companyId = null,
        int page = 1,
        int pageSize = 20,
        string? status = null)
    {
        var query = $"?page={page}&pageSize={pageSize}";

        if (status is not null)
        {
            query += $"&status={Uri.EscapeDataString(status)}";
        }

        var response = await ScopeClient.SendAsync(
            ScopeGet(url + query, userId, role, companyId));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<ListEnvelope>(response);

        Assert.NotNull(envelope?.Data);
        return envelope!;
    }

    private static IReadOnlyList<Guid> Ids(ListEnvelope envelope)
        => envelope.Data!.Items.Select(x => x.Id).ToList();

    // ------------------------------------------------------------------
    // Routing
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The literal extractions route is not captured by the {extractionId} route")]
    public async Task LiteralExtractionsRouteIsReachable()
    {
        await SeedExtractionAsync(TestDataSeeder.Ids.Admin);

        // This is the routing regression guard: 'extractions' must reach the
        // list action rather than being parsed as an extraction id.
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                AdminListUrl,
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"items\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"totalCount\"", raw, StringComparison.Ordinal);

        // The list payload must not carry a storage reference.
        Assert.DoesNotContain(
            "\"fileUrl\"",
            raw,
            StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Admin
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin sees pending extractions created by anyone")]
    public async Task AdminSeesPendingExtractions()
    {
        var byAdmin = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin);
        var byAgentA = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentA);
        var byAgentB = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentB);
        var legacy = await SeedExtractionAsync(null);

        var envelope = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin");

        var ids = Ids(envelope);

        Assert.Contains(byAdmin, ids);
        Assert.Contains(byAgentA, ids);
        Assert.Contains(byAgentB, ids);
        Assert.Contains(legacy, ids);
    }

    [Fact(DisplayName =
        "Admin list is paged and reports the full pending total")]
    public async Task AdminListIsPaged()
    {
        var ids = new List<Guid>();

        for (var i = 0; i < 3; i++)
        {
            ids.Add(await SeedExtractionAsync(TestDataSeeder.Ids.Admin));
        }

        var firstPage = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin",
            page: 1,
            pageSize: 2);

        Assert.Equal(1, firstPage.Data!.Page);
        Assert.Equal(2, firstPage.Data.PageSize);
        Assert.True(firstPage.Data.TotalCount >= 3);
        Assert.Equal(2, firstPage.Data.Items.Count);

        // TotalCount is the unpaged total, not the page size.
        Assert.True(firstPage.Data.TotalCount > firstPage.Data.Items.Count);
        Assert.Contains(firstPage.Data.Items[0].Id, ids);
    }

    [Fact(DisplayName =
        "Confirmed and expired extractions are excluded by default")]
    public async Task TerminalStatesAreExcludedByDefault()
    {
        var pending = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin,
            "Pending");
        var confirmed = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin,
            "Confirmed");
        var expired = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin,
            "Expired");

        var envelope = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin");

        var ids = Ids(envelope);

        Assert.Contains(pending, ids);
        Assert.DoesNotContain(confirmed, ids);
        Assert.DoesNotContain(expired, ids);
    }

    [Fact(DisplayName =
        "The status filter selects another state explicitly")]
    public async Task StatusFilterSelectsOtherState()
    {
        var confirmed = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin,
            "Confirmed");

        var envelope = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin",
            status: "Confirmed");

        Assert.Contains(confirmed, Ids(envelope));
        Assert.Equal("Confirmed", envelope.Data!.Items
            .First(x => x.Id == confirmed).Status);
    }

    [Fact(DisplayName =
        "Status matching ignores casing")]
    public async Task StatusFilterIgnoresCasing()
    {
        var confirmed = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin,
            "Confirmed");

        var envelope = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin",
            status: "confirmed");

        Assert.Contains(confirmed, Ids(envelope));
    }

    [Fact(DisplayName =
        "Soft-deleted extractions are excluded")]
    public async Task SoftDeletedIsExcluded()
    {
        var visible = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin);
        var deleted = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin,
            isDeleted: true);

        var envelope = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin");

        var ids = Ids(envelope);

        Assert.Contains(visible, ids);
        Assert.DoesNotContain(deleted, ids);
    }

    [Fact(DisplayName =
        "List items carry the review suggestions and state")]
    public async Task ListItemCarriesReviewData()
    {
        var id = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentA,
            failureCode: "MRZ_NOT_FOUND");

        var envelope = await ListAsync(
            AdminListUrl,
            TestDataSeeder.Ids.Admin,
            "Admin");

        var item = envelope.Data!.Items.Single(x => x.Id == id);

        Assert.Equal("upload.pdf", item.FileName);
        Assert.Equal("application/pdf", item.ContentType);
        Assert.Equal(2048L, item.FileSize);
        Assert.Equal("Passport", item.SuggestedType);
        Assert.Equal("D1-SUG-001", item.SuggestedDocumentNumber);
        Assert.Equal("Employee", item.SuggestedOwnerType);
        Assert.Equal("MRZ_NOT_FOUND", item.FailureCode);
        Assert.Equal("Pending", item.Status);
        Assert.NotNull(item.ExpiresAt);
        Assert.NotEqual(default, item.CreatedAt);
        Assert.Equal(TestDataSeeder.Ids.AgentA, item.CreatedBy);
    }

    // ------------------------------------------------------------------
    // Agent
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent sees its own pending extractions")]
    public async Task AgentSeesOwnExtractions()
    {
        var mine = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentA);

        var envelope = await ListAsync(
            AgentListUrl,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Contains(mine, Ids(envelope));
    }

    [Fact(DisplayName =
        "Agent does not see another agent's extraction")]
    public async Task AgentDoesNotSeeOtherAgentExtraction()
    {
        var otherAgents = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentB);

        var envelope = await ListAsync(
            AgentListUrl,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.DoesNotContain(otherAgents, Ids(envelope));
    }

    [Fact(DisplayName =
        "Agent does not see an Admin-created (non-legacy) extraction")]
    public async Task AgentDoesNotSeeAdminExtraction()
    {
        var adminExtraction = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin);

        var envelope = await ListAsync(
            AgentListUrl,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.DoesNotContain(adminExtraction, Ids(envelope));
    }

    [Fact(DisplayName =
        "Legacy NULL CreatedBy extractions stay visible and are flagged by a null creator")]
    public async Task LegacyNullCreatedByStaysVisible()
    {
        var legacy = await SeedExtractionAsync(null);

        var envelope = await ListAsync(
            AgentListUrl,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        var ids = Ids(envelope);

        // Not stranded: confirmation still accepts these from any in-scope
        // agent, so the list must surface them. The null CreatedBy is what
        // tells the UI this is a legacy row rather than the agent's own.
        Assert.Contains(legacy, ids);
        Assert.Null(envelope.Data!.Items
            .Single(x => x.Id == legacy).CreatedBy);
    }

    [Fact(DisplayName =
        "Agent list excludes another agent's terminal-state drafts")]
    public async Task AgentListRespectsStatus()
    {
        var minePending = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentA,
            "Pending");
        var mineConfirmed = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentA,
            "Confirmed");

        var pending = await ListAsync(
            AgentListUrl,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        var pendingIds = Ids(pending);

        Assert.Contains(minePending, pendingIds);
        Assert.DoesNotContain(mineConfirmed, pendingIds);

        var confirmed = await ListAsync(
            AgentListUrl,
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString(),
            status: "Confirmed");

        var confirmedIds = Ids(confirmed);

        Assert.Contains(mineConfirmed, confirmedIds);
        Assert.DoesNotContain(minePending, confirmedIds);
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    [Theory(DisplayName = "Invalid paging is rejected (400)")]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidPagingReturns400(int page, int pageSize)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"{AdminListUrl}?page={page}&pageSize={pageSize}",
                TestDataSeeder.Ids.Admin,
                "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Unknown status value is rejected (400)")]
    public async Task UnknownStatusReturns400()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"{AdminListUrl}?status=Pendingz",
                TestDataSeeder.Ids.Admin,
                "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "A client cannot reach the extraction list (403)")]
    public async Task ClientCannotListExtractions()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                AdminListUrl,
                TestDataSeeder.Ids.ClientUserA,
                "Client",
                TestDataSeeder.Ids.CompanyA.ToString()));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // ------------------------------------------------------------------
    // DTO information hygiene
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The extraction detail payload no longer exposes the storage reference")]
    public async Task ExtractionDetailOmitsFileUrl()
    {
        var extractionId = await SeedExtractionAsync(
            TestDataSeeder.Ids.Admin);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/documents/extractions/{extractionId}",
                TestDataSeeder.Ids.Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();

        // The dedicated file endpoint streams the bytes, so the internal
        // storage key must not be part of the review payload.
        Assert.DoesNotContain(
            "\"fileUrl\"",
            raw,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "extractions/",
            raw,
            StringComparison.OrdinalIgnoreCase);

        // The rest of the review contract is intact.
        Assert.Contains("\"suggestedType\"", raw, StringComparison.Ordinal);
        Assert.Contains(
            "\"suggestedDocumentNumber\"",
            raw,
            StringComparison.Ordinal);
        Assert.Contains("\"fields\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"failure\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"confidence\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"status\"", raw, StringComparison.Ordinal);
    }

    [Fact(DisplayName =
        "The agent extraction detail payload no longer exposes the storage reference")]
    public async Task AgentExtractionDetailOmitsFileUrl()
    {
        var extractionId = await SeedExtractionAsync(
            TestDataSeeder.Ids.AgentA);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/documents/extractions/{extractionId}",
                TestDataSeeder.Ids.AgentA,
                "Agent",
                TestDataSeeder.Ids.CompanyA.ToString()));

        await EnsureOk(response, HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            "extractions/",
            raw,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"fileName\"", raw, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Response envelopes
    // ------------------------------------------------------------------

    private sealed class ListEnvelope
    {
        public bool Success { get; set; }

        public ListPayload? Data { get; set; }
    }

    private sealed class ListPayload
    {
        public List<ItemPayload> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }
    }

    private sealed class ItemPayload
    {
        public Guid Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string? SuggestedType { get; set; }

        public string? SuggestedDocumentNumber { get; set; }

        public DateTime? SuggestedIssueDate { get; set; }

        public DateTime? SuggestedExpiryDate { get; set; }

        public string? SuggestedOwnerType { get; set; }

        public string? FailureCode { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid? CreatedBy { get; set; }
    }
}
