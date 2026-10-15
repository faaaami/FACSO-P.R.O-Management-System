using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// BE-2 regression: service contracts and the two invoice kinds carry genuine
/// calendar dates. Before the fix those properties were typed DateTime, so a
/// date-only JSON payload ("2026-06-01") bound to a DateTime with
/// DateTimeKind.Unspecified, which Npgsql refuses to write to a
/// timestamp with time zone column ("Cannot write DateTime with Kind=Unspecified
/// to PostgreSQL type 'timestamp with time zone'"). Every create therefore
/// failed at SaveChanges with a 500.
///
/// The fields are now DateOnly, end to end. These tests drive the real HTTP
/// endpoints with raw yyyy-MM-dd strings and assert both the create response
/// and the Dapper-backed read-back, so a regression to a timestamp type (or a
/// timezone shift on the way out) fails here rather than in production.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class CalendarDateOnlyBindingTests : IntegrationTestBase
{
    public CalendarDateOnlyBindingTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private const string InvoiceDate = "2026-06-01";
    private const string DueDate = "2026-06-15";
    private const string PeriodStart = "2026-06-01";
    private const string PeriodEnd = "2026-06-30";

    /// <summary>
    /// A task in the terminal Updated state that owns no service-fee invoice
    /// yet, so guard 4 (must be Updated) and guard 6 (one invoice per task) both
    /// pass and the create reaches persistence.
    /// </summary>
    private async Task<Guid> SeedUpdatedTaskWithoutInvoiceAsync()
    {
        var taskId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber =
                    $"DOC-DATEONLY-{documentId.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(30),
                FileUrl = $"https://storage.test/{documentId}",
                FileName = "passport.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = taskId,
                DocumentId = documentId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = TestDataSeeder.Ids.AgentA,
                Status = RenewalTaskStatus.Updated,
                ServiceFeeAmount = 2500m,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return taskId;
    }

    private static Guid IdFrom(string body)
        => Guid.Parse((string)JsonNode.Parse(body)!["data"]!["id"]!);

    private static void AssertDateOnly(
        string body,
        string property,
        string expected)
        => Assert.Contains(
            $"\"{property}\":\"{expected}\"",
            body);

    // ==================================================================
    // Retainer invoices
    // ==================================================================

    [Fact(DisplayName =
        "BE-2: a retainer invoice created from date-only JSON persists and reads back unshifted")]
    public async Task RetainerInvoice_DateOnlyJson_PersistsUnshifted()
    {
        var response = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/retainer-invoices",
            new
            {
                clientCompanyId = TestDataSeeder.Ids.CompanyA,
                serviceContractId = TestDataSeeder.Ids.ContractA,
                invoiceNumber = "RI-DATEONLY-1",
                invoiceDate = InvoiceDate,
                dueDate = DueDate,
                periodStart = PeriodStart,
                periodEnd = PeriodEnd,
                amount = 1000m,
                currency = "AED",
                notes = "date-only regression"
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var createBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertDateOnly(createBody, "invoiceDate", InvoiceDate);
        AssertDateOnly(createBody, "dueDate", DueDate);
        AssertDateOnly(createBody, "periodStart", PeriodStart);
        AssertDateOnly(createBody, "periodEnd", PeriodEnd);

        var id = IdFrom(createBody);

        var read = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/retainer-invoices/{id}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var readBody = await read.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        AssertDateOnly(readBody, "invoiceDate", InvoiceDate);
        AssertDateOnly(readBody, "dueDate", DueDate);
        AssertDateOnly(readBody, "periodStart", PeriodStart);
        AssertDateOnly(readBody, "periodEnd", PeriodEnd);
    }

    [Fact(DisplayName =
        "BE-2: a leap-day retainer invoice date round-trips without shifting")]
    public async Task RetainerInvoice_LeapDay_RoundTrips()
    {
        var response = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/retainer-invoices",
            new
            {
                clientCompanyId = TestDataSeeder.Ids.CompanyA,
                serviceContractId = TestDataSeeder.Ids.ContractA,
                invoiceNumber = "RI-LEAP-1",
                invoiceDate = "2028-02-29",
                dueDate = "2028-03-15",
                amount = 1000m,
                currency = "AED",
                notes = (string?)null
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertDateOnly(body, "invoiceDate", "2028-02-29");
        AssertDateOnly(body, "dueDate", "2028-03-15");
    }

    [Fact(DisplayName =
        "BE-2: a retainer invoice whose due date precedes its invoice date is rejected with 400")]
    public async Task RetainerInvoice_InvalidRange_IsBadRequest()
    {
        var response = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/retainer-invoices",
            new
            {
                clientCompanyId = TestDataSeeder.Ids.CompanyA,
                serviceContractId = TestDataSeeder.Ids.ContractA,
                invoiceNumber = "RI-BADRANGE-1",
                invoiceDate = "2026-06-15",
                dueDate = "2026-06-01",
                amount = 1000m,
                currency = "AED",
                notes = (string?)null
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================================================================
    // Service fee invoices
    // ==================================================================

    [Fact(DisplayName =
        "BE-2: a service fee invoice created from date-only JSON persists and reads back unshifted")]
    public async Task ServiceFeeInvoice_DateOnlyJson_PersistsUnshifted()
    {
        var taskId = await SeedUpdatedTaskWithoutInvoiceAsync();

        var response = await ScopeClient.SendAsync(ScopePost(
            "/api/v1/service-fee-invoices",
            new
            {
                clientCompanyId = TestDataSeeder.Ids.CompanyA,
                renewalTaskId = taskId,
                invoiceNumber = "SFI-DATEONLY-1",
                invoiceDate = InvoiceDate,
                dueDate = DueDate,
                amount = 2500m,
                currency = "AED",
                description = "date-only regression"
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var createBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertDateOnly(createBody, "invoiceDate", InvoiceDate);
        AssertDateOnly(createBody, "dueDate", DueDate);

        var id = IdFrom(createBody);

        var read = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/service-fee-invoices/{id}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var readBody = await read.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        AssertDateOnly(readBody, "invoiceDate", InvoiceDate);
        AssertDateOnly(readBody, "dueDate", DueDate);
    }

    // ==================================================================
    // Service contracts
    // ==================================================================

    [Fact(DisplayName =
        "BE-2: a service contract created and updated from date-only JSON persists and reads back unshifted")]
    public async Task ServiceContract_DateOnlyJson_CreatesUpdatesAndReads()
    {
        var create = await ScopeClient.SendAsync(ScopePost(
            $"/api/v1/clients/{TestDataSeeder.Ids.CompanyA}/contracts",
            new
            {
                contractNumber = "CT-DATEONLY-1",
                startDate = "2026-06-01",
                retainerAmount = 1000m,
                terms = "date-only regression"
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var createBody = await create.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        AssertDateOnly(createBody, "startDate", "2026-06-01");

        var id = IdFrom(createBody);

        var update = await ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/contracts/{id}",
            new
            {
                startDate = "2026-07-01",
                endDate = "2027-07-01",
                retainerAmount = 1200m,
                terms = "date-only regression (updated)"
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var read = await ScopeClient.SendAsync(ScopeGet(
            $"/api/v1/contracts/{id}",
            TestDataSeeder.Ids.Admin,
            "Admin"));

        var readBody = await read.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        AssertDateOnly(readBody, "startDate", "2026-07-01");
        AssertDateOnly(readBody, "endDate", "2027-07-01");
    }
}
