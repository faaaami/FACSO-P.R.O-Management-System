using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// D4: the two guards in ServiceFeeInvoiceCreationService that the manual
/// create endpoint enforces, which nothing covered before.
///
/// The same service is used by POST /api/v1/service-fee-invoices and by the
/// renewal-task completion event handler, so these cases pin the shared
/// rules rather than the endpoint:
///   guard 4  the renewal task must already be Updated;
///   guard 6  a task may own only one non-deleted service fee invoice.
///
/// Guard 6 is the invariant that makes the automatic completion path
/// recoverable at all. Because the completion is now atomic (D4 defect A), a
/// completed task either has its invoice or was never committed, so an admin
/// can always create the missing invoice by hand - but only if guard 6 keeps
/// a second invoice from being added to a task that already has one.
///
/// Both scenarios below are driven through the real HTTP endpoints; no
/// application state is injected.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ServiceFeeInvoiceCreationGuardTests
    : IntegrationTestBase
{
    private const decimal ServiceFee = 2500.00m;

    public ServiceFeeInvoiceCreationGuardTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<Guid> SeedDocumentAsync()
    {
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
                    $"DOC-GUARD-{documentId.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(10),
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

            db.SaveChanges();
        });

        return documentId;
    }

    private async Task<Guid> SeedApprovedTaskAsync(
        Guid documentId)
    {
        var taskId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = taskId,
                DocumentId = documentId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = TestDataSeeder.Ids.AgentA,
                Status = RenewalTaskStatus.Approved,
                ServiceFeeAmount = ServiceFee,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return taskId;
    }

    private Task<HttpResponseMessage> CompleteAsAgentAsync(
        Guid taskId)
        => ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/tasks/{taskId}/status",
            new
            {
                status = "Updated",
                renewedExpiryDate = "2030-09-30"
            },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

    private Task<HttpResponseMessage> CreateInvoiceAsync(
        Guid taskId,
        string invoiceNumber)
        => ScopeClient.SendAsync(ScopePost(
            "/api/v1/service-fee-invoices",
            new
            {
                clientCompanyId = TestDataSeeder.Ids.CompanyA,
                renewalTaskId = taskId,
                invoiceNumber,
                invoiceDate = "2026-09-30",
                dueDate = "2026-10-30",
                amount = ServiceFee,
                currency = "AED",
                description = "Manual invoice."
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

    private async Task<List<ServiceFeeInvoice>> InvoicesForTaskAsync(
        Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.ServiceFeeInvoices
                .IgnoreQueryFilters()
                .Where(x => x.RenewalTaskId == taskId)
                .ToListAsync();
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<int> CreatedAuditCountAsync()
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.AuditLogs
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.Action == "ServiceFeeInvoiceCreated");
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<RenewalTask> ReadTaskAsync(Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.RenewalTasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == taskId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ==================================================================
    // O. A completed task that already has an invoice
    // ==================================================================

    /// <summary>
    /// The task is driven to Updated through the real completion endpoint,
    /// which leaves it owning exactly one invoice. A second manual create must
    /// then be refused by guard 6, and nothing may be duplicated.
    /// </summary>
    [Fact(DisplayName =
        "D4-O: a manual create for a completed task that already has an invoice is 409 and creates no second invoice")]
    public async Task DuplicateInvoiceForCompletedTask_IsRejected()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        // Real completion: the task becomes Updated and gets its invoice.
        var completion = await CompleteAsAgentAsync(taskId);
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);

        var afterCompletion =
            Assert.Single(await InvoicesForTaskAsync(taskId));
        var auditsAfterCompletion =
            await CreatedAuditCountAsync();

        // A second invoice for the same task must be refused.
        var response =
            await CreateInvoiceAsync(taskId, "SFI-MANUAL-DUPLICATE");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already exists", body);

        // The original invoice is untouched and still the only one.
        var invoices =
            await InvoicesForTaskAsync(taskId);
        var stillOne = Assert.Single(invoices);
        Assert.Equal(afterCompletion.Id, stillOne.Id);
        Assert.Equal(
            afterCompletion.InvoiceNumber, stillOne.InvoiceNumber);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, stillOne.Status);
        Assert.Null(stillOne.PaidAt);

        // A refused create writes no audit row.
        Assert.Equal(auditsAfterCompletion, await CreatedAuditCountAsync());
    }

    // ==================================================================
    // P. A task that is not completed
    // ==================================================================

    /// <summary>
    /// Guard 4: the task must already be Updated. An Approved task is not
    /// billable, so the create is refused before any write.
    /// </summary>
    [Fact(DisplayName =
        "D4-P: a manual create for a task that is not Updated is 409 and creates no invoice")]
    public async Task CreateForNonCompletedTask_IsRejected()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        var auditsBefore = await CreatedAuditCountAsync();

        var response =
            await CreateInvoiceAsync(taskId, "SFI-MANUAL-NOT-COMPLETED");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("must be completed", body);

        // No invoice and no audit row were written.
        Assert.Empty(await InvoicesForTaskAsync(taskId));
        Assert.Equal(auditsBefore, await CreatedAuditCountAsync());

        // The task was not advanced as a side effect.
        Assert.Equal(
            RenewalTaskStatus.Approved,
            (await ReadTaskAsync(taskId)).Status);
    }

    /// <summary>
    /// The same guard for a task further up the chain, so the rule is pinned
    /// as "must be Updated" rather than "must not be Submitted".
    /// </summary>
    [Fact(DisplayName =
        "D4-P2: a manual create for a Submitted task is 409 and creates no invoice")]
    public async Task CreateForSubmittedTask_IsRejected()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == taskId);
            task.Status = RenewalTaskStatus.Submitted;
            db.SaveChanges();
        });

        var response =
            await CreateInvoiceAsync(taskId, "SFI-MANUAL-SUBMITTED");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("must be completed",
            await response.Content.ReadAsStringAsync());

        Assert.Empty(await InvoicesForTaskAsync(taskId));
    }
}
