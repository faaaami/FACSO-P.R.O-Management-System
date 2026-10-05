using System.Net;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Consistency;

/// <summary>
/// M3 404-consistency verification. Locked behavior:
///  - F1: a soft-deleted document scan returns 404 (admin + agent), while a
///    live document that has no associated file keeps the existing 409.
///  - F2: Agent child-list endpoints (clients/entities, employees/documents)
///    return 404 for an out-of-scope or nonexistent parent, never 200-empty.
///  - F3: Admin child-list endpoints return 404 for a nonexistent parent,
///    never 200-empty.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NotFoundConsistencyTests : IntegrationTestBase
{
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;

    public NotFoundConsistencyTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // F1 - soft-deleted document scan 404; live-doc/no-file stays 409
    // ==================================================================

    private Task SoftDeleteDocumentAsync(Guid documentId)
        => WithScopeDbContextAsync(db =>
        {
            var doc = db.Documents.Single(x => x.Id == documentId);
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

    [Fact(DisplayName =
        "F1: admin scan of a soft-deleted document returns 404")]
    public async Task AdminScan_SoftDeletedDocument_Returns404()
    {
        await SoftDeleteDocumentAsync(TestDataSeeder.Ids.DocumentA);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/documents/{TestDataSeeder.Ids.DocumentA}/scan",
                Admin,
                role: "Admin"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "F1: agent scan of a soft-deleted in-scope document returns 404")]
    public async Task AgentScan_SoftDeletedDocument_Returns404()
    {
        await SoftDeleteDocumentAsync(TestDataSeeder.Ids.DocumentA);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/documents/{TestDataSeeder.Ids.DocumentA}/scan",
                AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "F1 control: scan of a live document without a file keeps 409")]
    public async Task Scan_LiveDocumentWithoutFile_Returns409()
    {
        var liveDocId = Guid.NewGuid();
        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = liveDocId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.Passport,
                DocumentNumber = "DOC-NO-FILE",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(4),
                FileUrl = "",
                FileName = "doc.pdf",
                ContentType = "application/pdf",
                FileSize = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/documents/{liveDocId}/scan",
                Admin,
                role: "Admin"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ==================================================================
    // F2 - Agent child lists: out-of-scope or nonexistent parent -> 404
    // ==================================================================

    public static IEnumerable<object[]> AgentOutOfScopeOrUnknown()
    {
        // Out-of-scope company (Agent B's world) and nonexistent company.
        yield return new object[]
        {
            $"/api/v1/agent/clients/{TestDataSeeder.Ids.CompanyB}/entities"
        };
        yield return new object[]
        {
            $"/api/v1/agent/clients/{TestDataSeeder.Ids.Unknown}/entities"
        };

        // Out-of-scope employee (Agent B's world) and nonexistent employee.
        yield return new object[]
        {
            $"/api/v1/agent/employees/{TestDataSeeder.Ids.EmployeeB}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/agent/employees/{TestDataSeeder.Ids.Unknown}/documents"
        };
    }

    [Theory(DisplayName =
        "F2: agent child list returns 404 for out-of-scope/nonexistent parent")]
    [MemberData(nameof(AgentOutOfScopeOrUnknown))]
    public async Task AgentChildList_OutOfScopeOrUnknownParent_Returns404(
        string url)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, AgentA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static IEnumerable<object[]> AgentInScopeParent()
    {
        yield return new object[]
        {
            $"/api/v1/agent/clients/{TestDataSeeder.Ids.CompanyA}/entities"
        };
        yield return new object[]
        {
            $"/api/v1/agent/employees/{TestDataSeeder.Ids.EmployeeA}/documents"
        };
    }

    [Theory(DisplayName =
        "F2 control: agent child list returns 200 for in-scope parent")]
    [MemberData(nameof(AgentInScopeParent))]
    public async Task AgentChildList_InScopeParent_Returns200(string url)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, AgentA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ==================================================================
    // F3 - Admin child lists: nonexistent parent -> 404
    // ==================================================================

    public static IEnumerable<object[]> AdminChildListUnknown()
    {
        var unknown = TestDataSeeder.Ids.Unknown;

        yield return new object[]
        {
            $"/api/v1/admin/entities/{unknown}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/admin/employees/{unknown}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/admin/employees/{unknown}/timeline"
        };
        yield return new object[]
        {
            $"/api/v1/admin/clients/{unknown}/entities"
        };
        yield return new object[]
        {
            $"/api/v1/admin/clients/{unknown}/contacts"
        };
    }

    [Theory(DisplayName =
        "F3: admin child list returns 404 for nonexistent parent")]
    [MemberData(nameof(AdminChildListUnknown))]
    public async Task AdminChildList_UnknownParent_Returns404(string url)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, Admin, role: "Admin"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static IEnumerable<object[]> AdminChildListKnown()
    {
        yield return new object[]
        {
            $"/api/v1/admin/entities/{TestDataSeeder.Ids.EntityA}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/admin/employees/{TestDataSeeder.Ids.EmployeeA}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/admin/employees/{TestDataSeeder.Ids.EmployeeA}/timeline"
        };
        yield return new object[]
        {
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/entities"
        };
        yield return new object[]
        {
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/contacts"
        };
    }

    [Theory(DisplayName =
        "F3 control: admin child list returns 200 for known parent")]
    [MemberData(nameof(AdminChildListKnown))]
    public async Task AdminChildList_KnownParent_Returns200(string url)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, Admin, role: "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}