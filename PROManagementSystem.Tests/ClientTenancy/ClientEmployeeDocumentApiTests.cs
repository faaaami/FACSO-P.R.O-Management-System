using System.Net;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ClientTenancy;

/// <summary>
/// Week 3 Day 2: Client employees + documents (read-only mirror).
///
/// Locked behavior:
///  - The tenant is ALWAYS derived from the authenticated user's
///    ClientCompanyId claim and enforced inside the SQL queries.
///  - By-id and parent-navigation endpoints return 404 for cross-company or
///    unknown resources (never 403, never 200-empty).
///  - The employees list entityId filter is a pure SQL filter: an
///    out-of-company entity yields an empty paginated 200 and never reveals
///    whether the entity exists.
///  - A client-supplied clientCompanyId parameter cannot override the tenant.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientEmployeeDocumentApiTests : IntegrationTestBase
{
    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid ClientB = TestDataSeeder.Ids.ClientUserB;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;

    private static readonly string CompanyA =
        TestDataSeeder.Ids.CompanyA.ToString();
    private static readonly string CompanyB =
        TestDataSeeder.Ids.CompanyB.ToString();

    public ClientEmployeeDocumentApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Employees list
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "E1: Client lists only their company's employees (200)")]
    public async Task Employees_Own_OnlyOwnWorld()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/employees", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.EmployeeA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.EmployeeB.ToString(), body);
    }

    [Fact(DisplayName =
        "E2: Client employees filterable by own entity (200)")]
    public async Task Employees_FilterByOwnEntity_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/employees?entityId={TestDataSeeder.Ids.EntityA}",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.EmployeeA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.EmployeeB.ToString(), body);
    }

    [Fact(DisplayName =
        "E3: entity filter for another company's entity is a pure filter (empty 200)")]
    public async Task Employees_FilterByOtherCompanyEntity_ReturnsEmpty200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/employees?entityId={TestDataSeeder.Ids.EntityB}",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"totalCount\":0", body);
        Assert.DoesNotContain(TestDataSeeder.Ids.EmployeeA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.EmployeeB.ToString(), body);
    }

    [Fact(DisplayName =
        "E4: Client-supplied clientCompanyId cannot override the tenant")]
    public async Task Employees_SuppliedClientCompanyId_Ignored()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/employees?clientCompanyId={CompanyB}",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.EmployeeA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.EmployeeB.ToString(), body);
    }

    // ------------------------------------------------------------------
    // Employees / documents by id — own resources -> 200
    // ------------------------------------------------------------------

    public static IEnumerable<object[]> OwnResources()
    {
        yield return new object[]
        {
            $"/api/v1/client/employees/{TestDataSeeder.Ids.EmployeeA}"
        };
        yield return new object[]
        {
            $"/api/v1/client/employees/{TestDataSeeder.Ids.EmployeeA}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/client/documents/{TestDataSeeder.Ids.DocumentA}"
        };
        yield return new object[]
        {
            $"/api/v1/client/documents/{TestDataSeeder.Ids.DocumentA}/scan"
        };
    }

    [Theory(DisplayName =
        "E5: Client reads their own employee/document resources (200)")]
    [MemberData(nameof(OwnResources))]
    public async Task ClientOwnResource_Returns200(string url)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    // Cross-company / unknown resources -> 404, never 403
    // ------------------------------------------------------------------

    public static IEnumerable<object[]> CrossCompanyOrUnknownResources()
    {
        var unknown = TestDataSeeder.Ids.Unknown;

        yield return new object[]
        {
            $"/api/v1/client/employees/{TestDataSeeder.Ids.EmployeeB}"
        };
        yield return new object[]
        {
            $"/api/v1/client/employees/{unknown}"
        };
        yield return new object[]
        {
            $"/api/v1/client/employees/{TestDataSeeder.Ids.EmployeeB}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/client/employees/{unknown}/documents"
        };
        yield return new object[]
        {
            $"/api/v1/client/documents/{TestDataSeeder.Ids.DocumentB}"
        };
        yield return new object[]
        {
            $"/api/v1/client/documents/{unknown}"
        };
        yield return new object[]
        {
            $"/api/v1/client/documents/{TestDataSeeder.Ids.DocumentB}/scan"
        };
        yield return new object[]
        {
            $"/api/v1/client/documents/{unknown}/scan"
        };
    }

    [Theory(DisplayName =
        "E6: Cross-company or unknown employee/document resource returns 404")]
    [MemberData(nameof(CrossCompanyOrUnknownResources))]
    public async Task ClientResource_CrossCompanyOrUnknown_Returns404(
        string url)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(url, ClientA,
                role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "E7: Client employee documents contain their own documents only")]
    public async Task Employees_Documents_Own_ContainsOwnDocs()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/employees/{TestDataSeeder.Ids.EmployeeA}/documents",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("DOC-A-001", body);
        Assert.DoesNotContain("DOC-B-001", body);
    }

    // ------------------------------------------------------------------
    // Documents list + filters
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D1: Client documents list only their company's documents (200)")]
    public async Task Documents_Own_OnlyOwnWorld()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.DocumentB.ToString(), body);
    }

    [Fact(DisplayName =
        "D2: Client documents filterable by document type")]
    public async Task Documents_FilterByType_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents?type=Passport", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
    }

    [Fact(DisplayName =
        "D3: Client documents filterable by status (ExpiringSoon)")]
    public async Task Documents_FilterByStatus_ExpiringSoon_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents?status=ExpiringSoon", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
    }

    [Fact(DisplayName =
        "D4: Client documents filterable by expiry range")]
    public async Task Documents_FilterByExpiryRange_Returns200()
    {
        var from = Uri.EscapeDataString(
            DateTime.UtcNow.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(
            DateTime.UtcNow.AddDays(60).ToString("O"));

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/documents?expiresFrom={from}&expiresTo={to}",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
    }

    [Fact(DisplayName =
        "D5: Client documents filterable by owner type (Employee)")]
    public async Task Documents_FilterByOwnerType_Employee_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents?ownerType=Employee", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.DocumentB.ToString(), body);
    }

    [Fact(DisplayName =
        "D6: Client documents combine entity-owned and employee-owned documents")]
    public async Task Documents_CombinesEntityAndEmployeeOwned_Returns200()
    {
        var entityOwnedId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = entityOwnedId,
                ClientEntityId = TestDataSeeder.Ids.EntityA,
                Type = DocumentType.TradeLicense,
                DocumentNumber = "DOC-ENT-A",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(2),
                FileUrl = "https://storage.test/entity",
                FileName = "licence.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(entityOwnedId.ToString(), body);
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
    }

    [Fact(DisplayName =
        "D7: Owner type Entity returns only entity-owned documents")]
    public async Task Documents_FilterByOwnerType_Entity_ReturnsOnlyEntityOwned()
    {
        var entityOwnedId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = entityOwnedId,
                ClientEntityId = TestDataSeeder.Ids.EntityA,
                Type = DocumentType.TradeLicense,
                DocumentNumber = "DOC-ENT-B",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddYears(2),
                FileUrl = "https://storage.test/entity",
                FileName = "licence.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents?ownerType=Entity", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(entityOwnedId.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.DocumentA.ToString(), body);
    }

    // ------------------------------------------------------------------
    // Document scan
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "D8: Client can view the scan for their own document (200)")]
    public async Task DocumentsScan_Own_ContainsFileUrl()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/documents/{TestDataSeeder.Ids.DocumentA}/scan",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("https://storage.test/dummy", body);
    }

    [Fact(DisplayName =
        "D9: Client scan of a soft-deleted document returns 404")]
    public async Task DocumentsScan_SoftDeleted_Returns404()
    {
        await WithScopeDbContextAsync(db =>
        {
            var doc = db.Documents.Single(x => x.Id == TestDataSeeder.Ids.DocumentA);
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/documents/{TestDataSeeder.Ids.DocumentA}/scan",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "D10: Client scan of a document without a file returns 409")]
    public async Task DocumentsScan_NoFile_Returns409()
    {
        var noFileId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = noFileId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.Passport,
                DocumentNumber = "DOC-NO-FILE-CLIENT",
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
                $"/api/v1/client/documents/{noFileId}/scan",
                ClientA,
                role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Expiring documents
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "X1: Client expiring documents are scoped to their company (days=30)")]
    public async Task DocumentsExpiring_Days30_OnlyOwn()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents/expiring?days=30", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.DocumentA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.DocumentB.ToString(), body);
    }

    public static IEnumerable<object[]> AllowedDays()
    {
        yield return new object[] { 60 };
        yield return new object[] { 90 };
    }

    [Theory(DisplayName =
        "X2: Client expiring documents accept days=60 and days=90")]
    [MemberData(nameof(AllowedDays))]
    public async Task DocumentsExpiring_AllowedDays_Returns200(int days)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/client/documents/expiring?days={days}", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "X3: Client expiring documents reject disallowed days (400)")]
    public async Task DocumentsExpiring_InvalidDays_Returns400()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents/expiring?days=120", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION_ERROR", body);
    }

    [Fact(DisplayName =
        "X4: Expiring documents never cross the tenant boundary")]
    public async Task DocumentsExpiring_TenantScoped()
    {
        // An expiring document owned by Company B's employee.
        var companyBDocId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Documents.Add(new Document
            {
                Id = companyBDocId,
                EmployeeId = TestDataSeeder.Ids.EmployeeB,
                Type = DocumentType.EmiratesId,
                DocumentNumber = "DOC-B-EXTRA",
                IssueDate = DateTime.UtcNow.AddYears(-1),
                ExpiryDate = DateTime.UtcNow.AddDays(5),
                FileUrl = "https://storage.test/b-extra",
                FileName = "extra.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        // Client A (Company A) must not see Company B's expiring document.
        var responseA = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents/expiring?days=30", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(responseA, HttpStatusCode.OK);

        var bodyA = await responseA.Content.ReadAsStringAsync();
        Assert.DoesNotContain(companyBDocId.ToString(), bodyA);

        // Client B (Company B) must see it.
        var responseB = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/documents/expiring?days=30", ClientB,
                role: "Client", clientCompanyId: CompanyB));

        await EnsureOk(responseB, HttpStatusCode.OK);

        var bodyB = await responseB.Content.ReadAsStringAsync();
        Assert.Contains(companyBDocId.ToString(), bodyB);
    }

    // ------------------------------------------------------------------
    // Role gates on the new Client endpoints
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A1: Agent JWT on a Client endpoint returns 403")]
    public async Task AgentToken_OnClientEndpoint_Returns403()
    {
        var token = JwtHelper.MintToken(
            AgentA, "Agent",
            "agent-a@test.local", "Agent A",
            clientCompanyId: CompanyA);

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/client/employees", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "A2: Unauthenticated request to a Client endpoint returns 401")]
    public async Task NoToken_OnClientEndpoint_Returns401()
    {
        var response = await AuthClient.GetAsync(
            "/api/v1/client/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}