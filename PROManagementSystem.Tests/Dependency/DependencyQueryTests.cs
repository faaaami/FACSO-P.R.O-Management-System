using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Dependency;

/// <summary>
/// M4 verification. The Agent dependency endpoint now runs its scope predicate
/// in SQL (EXISTS over task-linked, non-deleted documents) instead of
/// materializing the full set of agent-scoped document IDs and pushing it back
/// as an IN list. These tests lock identical behavior: same rows, ordering,
/// TotalCount, 404 semantics, and exact response fields.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DependencyQueryTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid Unknown = TestDataSeeder.Ids.Unknown;

    // Runtime-generated docs (each linked to Employee A so they can be put in
    // Agent A's task-linked scope) used as dependency targets.
    private static readonly Guid DocC = Guid.Parse("00000000-0000-0000-0000-00000000c001");
    private static readonly Guid DocD = Guid.Parse("00000000-0000-0000-0000-00000000d001");
    private static readonly Guid TaskC = Guid.Parse("00000000-0000-0000-0000-0000000000c0");
    private static readonly Guid TaskD = Guid.Parse("00000000-0000-0000-0000-0000000000d0");

    private static readonly DateTime BaseNow = DateTime.UtcNow;

    public DependencyQueryTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    /// <summary>Seeds DocC + DocD, each in Agent A's task-linked scope.</summary>
    private Task SeedInScopeDocumentsAsync()
        => WithScopeDbContextAsync(db =>
        {
            db.Documents.AddRange(
                ScopedDocument(DocC, "DOC-C-001", BaseNow.AddDays(-1)),
                ScopedDocument(DocD, "DOC-D-001", BaseNow.AddDays(-2)));

            db.RenewalTasks.AddRange(
                AgentTask(TaskC, DocC, "TK-C-001", createdAt: BaseNow.AddDays(-1)),
                AgentTask(TaskD, DocD, "TK-D-001", createdAt: BaseNow.AddDays(-2)));

            db.SaveChanges();
        });

    /// <summary>Adds a dependency row from <paramref name="parent"/> onto <paramref name="target"/>.</summary>
    private Task AddDependencyAsync(Guid parentId, Guid targetId, DateTime createdAt)
        => WithScopeDbContextAsync(db =>
        {
            db.DocumentDependencies.Add(new DocumentDependency
            {
                Id = Guid.NewGuid(),
                DocumentId = parentId,
                DependsOnDocumentId = targetId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

    private async Task<HttpStatusCode> GetAsync(
        string url, Guid userId, string role, Guid? clientCompanyId)
        => (await ScopeClient.SendAsync(
            ScopeGet(url, userId, role, clientCompanyId?.ToString()))).StatusCode;

    /// <summary>Returns the parsed items array for an admin/agent dependency GET.</summary>
    private async Task<JsonArray?> GetItemsAsync(
        string url, Guid userId, string role)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, userId, role, clientCompanyId: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return body["data"]!["items"]!.AsArray();
    }

    private static Document ScopedDocument(
        Guid id, string docNumber, DateTime now)
        => new()
        {
            Id = id,
            EmployeeId = TestDataSeeder.Ids.EmployeeA,
            Type = DocumentType.Passport,
            DocumentNumber = docNumber,
            IssueDate = now.AddYears(-1),
            ExpiryDate = now.AddYears(5),
            FileUrl = "https://storage.test/m4",
            FileName = "dep.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

    private static RenewalTask AgentTask(
        Guid id, Guid docId, string reference, DateTime createdAt)
        => new()
        {
            Id = id,
            DocumentId = docId,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            AssignedStaffId = AgentA,
            Status = RenewalTaskStatus.Submitted,
            ServiceFeeAmount = 150m,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsDeleted = false,
            DeletedAt = null
        };

    // ==================================================================
    // Tests
    // ==================================================================

    [Fact(DisplayName =
        "T1: Agent reads dependencies of an in-scope document (exact fields, 200)")]
    public async Task Agent_InScopeWithDependency_Returns200()
    {
        await SeedInScopeDocumentsAsync();
        await AddDependencyAsync(
            TestDataSeeder.Ids.DocumentA, DocC, BaseNow.AddMinutes(-1));

        var items = await GetItemsAsync(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}/dependencies",
            AgentA,
            "Agent");

        var item = Assert.Single(items!);
        Assert.Equal("DOC-C-001", (string)item!["documentNumber"]!);
        Assert.Equal("Passport", (string)item!["documentType"]!);
        Assert.Equal(DocC, Guid.Parse((string)item!["dependsOnDocumentId"]!));
        Assert.True((bool)item["isActive"]!);
        Assert.False((bool)item["isDeleted"]!);
    }

    [Fact(DisplayName =
        "T2: Agent reading dependencies of an out-of-scope document returns 404")]
    public async Task Agent_OutOfScopeDocument_Returns404()
    {
        var status = await GetAsync(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentB}/dependencies",
            AgentA,
            "Agent",
            null);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact(DisplayName =
        "T3: Agent reading dependencies of a nonexistent document returns 404")]
    public async Task Agent_NonexistentDocument_Returns404()
    {
        var status = await GetAsync(
            $"/api/v1/agent/documents/{Unknown}/dependencies",
            AgentA,
            "Agent",
            null);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact(DisplayName =
        "T4: Admin reads dependencies (exact fields, 200)")]
    public async Task Admin_Dependencies_Returns200()
    {
        await SeedInScopeDocumentsAsync();
        await AddDependencyAsync(
            TestDataSeeder.Ids.DocumentA, DocC, BaseNow.AddMinutes(-1));

        var items = await GetItemsAsync(
            $"/api/v1/admin/documents/{TestDataSeeder.Ids.DocumentA}/dependencies",
            Admin,
            "Admin");

        var item = Assert.Single(items!);
        Assert.Equal("DOC-C-001", (string)item!["documentNumber"]!);
        Assert.Equal(DocC, Guid.Parse((string)item!["dependsOnDocumentId"]!));
    }

    [Fact(DisplayName =
        "T5: Admin reading dependencies of a nonexistent document returns 404")]
    public async Task Admin_NonexistentDocument_Returns404()
    {
        var status = await GetAsync(
            $"/api/v1/admin/documents/{Unknown}/dependencies",
            Admin,
            "Admin",
            null);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact(DisplayName =
        "T6: Document with zero dependencies returns an empty set (200)")]
    public async Task Admin_ZeroDependencies_ReturnsEmpty()
    {
        await SeedInScopeDocumentsAsync();

        var items = await GetItemsAsync(
            $"/api/v1/admin/documents/{TestDataSeeder.Ids.DocumentB}/dependencies",
            Admin,
            "Admin");

        Assert.Empty(items!);
    }

    [Fact(DisplayName =
        "T7: Admin returns multiple dependencies ordered by CreatedAt (200)")]
    public async Task Admin_MultipleDependencies_ReturnsOrdered()
    {
        await SeedInScopeDocumentsAsync();
        await AddDependencyAsync(
            TestDataSeeder.Ids.DocumentA, DocC, BaseNow.AddMinutes(-10));
        await AddDependencyAsync(
            TestDataSeeder.Ids.DocumentA, DocD, BaseNow.AddMinutes(-1));

        var items = await GetItemsAsync(
            $"/api/v1/admin/documents/{TestDataSeeder.Ids.DocumentA}/dependencies",
            Admin,
            "Admin");

        Assert.Equal(2, items!.Count);
        Assert.Equal("DOC-D-001", (string)items[0]!["documentNumber"]!);
        Assert.Equal("DOC-C-001", (string)items[1]!["documentNumber"]!);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/documents/{TestDataSeeder.Ids.DocumentA}/dependencies",
                Admin,
                role: "Admin",
                clientCompanyId: null));
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(2, (int)body["data"]!["totalCount"]!);
    }

    [Fact(DisplayName =
        "T8: Agent with mixed-scope dependency targets returns only the in-scope ones (200)")]
    public async Task Agent_MixedScopeTargets_ReturnsOnlyInScope()
    {
        await SeedInScopeDocumentsAsync();
        // Document A depends on Doc C (in Agent A scope) AND Document B
        // (out of Agent A scope). The SQL-side scope predicate must drop B.
        await AddDependencyAsync(
            TestDataSeeder.Ids.DocumentA, DocC, BaseNow.AddMinutes(-1));
        await AddDependencyAsync(
            TestDataSeeder.Ids.DocumentA, TestDataSeeder.Ids.DocumentB, BaseNow.AddMinutes(-2));

        var items = await GetItemsAsync(
            $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}/dependencies",
            AgentA,
            "Agent");

        var item = Assert.Single(items!);
        Assert.Equal("DOC-C-001", (string)item!["documentNumber"]!);
    }
}