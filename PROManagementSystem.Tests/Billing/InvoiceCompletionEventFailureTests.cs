using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// D4-10: the atomicity boundary of renewal task completion.
///
/// Completing a task writes the task status, the document expiry, a history
/// row, two audit rows, the agent notification, the client notification and
/// the service fee invoice raised by the completion event handler. D4 puts all
/// of that in ONE transaction
/// (UpdateRenewalTaskStatusCommandHandler.cs step 4), so if invoice creation
/// fails the completion is rolled back whole: the task stays Approved, the
/// document expiry is untouched, and no history row, no audit row and no
/// invoice survive. The request is then fully retryable, which is the point -
/// under the previous two-save arrangement the task was already terminal when
/// invoice creation failed, so replaying the request was refused as "already
/// completed" and the missing invoice could never be produced by retrying.
///
/// ON REACHABILITY, stated honestly:
/// The two failures exercised here are produced by seeding a pre-existing
/// invoice that the handler's own guards then reject. They are NOT states the
/// application can normally reach - the manual create endpoint runs the same
/// guards, so no task can be Approved and already own an invoice, and the
/// generated invoice number only collides if two task GUIDs share their first
/// eight hex characters. They are used here because they are deterministic,
/// already available through the test DbContext, and they exercise the real
/// exception path out of the real event handler - which is exactly the
/// rollback coverage this boundary needs. The genuinely reachable race (two
/// concurrent completions) is covered separately in
/// Concurrency/RenewalTaskCompletionConcurrencyTests.cs.
///
/// What these tests assert is the corrected behaviour: nothing is committed
/// when the invoice cannot be created.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class InvoiceCompletionEventFailureTests
    : IntegrationTestBase
{
    private const decimal ServiceFee = 2500.00m;

    public InvoiceCompletionEventFailureTests(DatabaseFixture fixture)
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
                    $"DOC-INVFAIL-{documentId.ToString("N")[..8]}",
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

    /// <summary>
    /// A task already in Approved with a fee set and the agent assigned, so
    /// these tests exercise the invoice seam rather than re-testing the status
    /// chain.
    /// </summary>
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

    /// <summary>
    /// Writes an invoice directly, bypassing the creation service's guards.
    /// This is the test seam used to stage the collisions below.
    /// </summary>
    private async Task<Guid> SeedInvoiceAsync(
        Guid taskId,
        string invoiceNumber,
        decimal amount = ServiceFee)
    {
        var invoiceId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.ServiceFeeInvoices.Add(new ServiceFeeInvoice
            {
                Id = invoiceId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                RenewalTaskId = taskId,
                InvoiceNumber = invoiceNumber,
                InvoiceDate = now,
                DueDate = now.AddDays(30),
                Amount = amount,
                Currency = "AED",
                Status = ServiceFeeInvoiceStatus.Pending,
                Description = "Seeded for the invoice completion seam.",
                PaidAt = null,
                VoidedAt = null,
                VoidReason = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        return invoiceId;
    }

    private Task<HttpResponseMessage> CompleteAsync(Guid taskId)
        => ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/agent/tasks/{taskId}/status",
            new
            {
                status = "Updated",
                renewedExpiryDate = "2030-09-30"
            },
            TestDataSeeder.Ids.AgentA,
            "Agent"));

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

    private async Task<DateTime> ReadDocumentExpiryAsync(
        Guid documentId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return (await db.Documents
                .Where(x => x.Id == documentId)
                .Select(x => x.ExpiryDate)
                .FirstAsync())!.Value;
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<int> HistoryCountAsync(Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.RenewalTaskHistories
                .IgnoreQueryFilters()
                .CountAsync(x => x.RenewalTaskId == taskId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<int> CompletionAuditCountAsync(
        string action,
        Guid entityId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.AuditLogs
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.Action == action && x.EntityId == entityId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    /// <summary>
    /// Counts the creation audits belonging to one renewal task.
    ///
    /// ServiceFeeInvoiceCreated records the INVOICE as its EntityId, not the
    /// task, so it cannot be counted by task id the way the status and
    /// document audits can. The service writes the task id into the
    /// description, so matching on that is what makes this check able to
    /// actually observe a rolled-back invoice creation instead of trivially
    /// counting zero.
    /// </summary>
    private async Task<int> InvoiceCreatedAuditCountForTaskAsync(
        Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.AuditLogs
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.Action == "ServiceFeeInvoiceCreated" &&
                    x.EntityType == "ServiceFeeInvoice" &&
                    x.Description != null &&
                    x.Description.Contains(taskId.ToString()));
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    /// <summary>
    /// Asserts that nothing at all from the failed completion survived.
    /// Counts are captured before the attempt so the seeded rows the test
    /// created itself are excluded.
    /// </summary>
    private async Task AssertNothingCommittedAsync(
        Guid taskId,
        Guid documentId,
        DateTime originalExpiry,
        int historyBefore,
        int statusAuditsBefore,
        int invoiceAuditsBefore)
    {
        // The task is not terminal and was not stamped as completed.
        var task = await ReadTaskAsync(taskId);
        Assert.Equal(RenewalTaskStatus.Approved, task.Status);
        Assert.Null(task.CompletedAt);

        // The document expiry did not move.
        Assert.Equal(
            originalExpiry,
            await ReadDocumentExpiryAsync(documentId));

        // No history row, no status audit, no invoice audit.
        Assert.Equal(historyBefore, await HistoryCountAsync(taskId));
        Assert.Equal(
            statusAuditsBefore,
            await CompletionAuditCountAsync(
                "RenewalTaskStatusUpdated", taskId));
        Assert.Equal(
            invoiceAuditsBefore,
            await InvoiceCreatedAuditCountForTaskAsync(taskId));
    }

    private static void AssertConflict(
        HttpResponseMessage response,
        string stage)
        => Assert.True(
            response.StatusCode == HttpStatusCode.Conflict,
            $"{stage}: expected 409 Conflict but got " +
            $"{(int)response.StatusCode}.");

    // ==================================================================
    // 5-1: the task already has an invoice -> nothing is committed
    // ==================================================================

    [Fact(DisplayName =
        "D4-10.1: when invoice creation is refused, the completion is rolled back whole and the task stays Approved")]
    public async Task ExistingInvoice_CompletionIsRolledBack()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        // A Pending invoice for this task already exists, so the handler's
        // "one active invoice per renewal task" guard will fire.
        var seededInvoiceId =
            await SeedInvoiceAsync(taskId, "SFI-PRE-EXISTING-001");

        var expiryBefore =
            await ReadDocumentExpiryAsync(documentId);
        var historyBefore = await HistoryCountAsync(taskId);

        // The seeded invoice's own audit count is irrelevant; what matters is
        // that the failed attempt adds none.
        var invoiceAuditsBefore =
            await CompletionAuditCountAsync(
                "ServiceFeeInvoiceCreated", taskId);
        var statusAuditsBefore =
            await CompletionAuditCountAsync(
                "RenewalTaskStatusUpdated", taskId);

        var response = await CompleteAsync(taskId);
        var body = await response.Content.ReadAsStringAsync();

        // The caller is told the completion failed...
        AssertConflict(response, "complete");
        Assert.Contains("already exists", body);

        // ...and nothing was committed. The task is still Approved, so this
        // is the opposite of the previous behaviour, where the status change
        // was durable and only the invoice was missing.
        await AssertNothingCommittedAsync(
            taskId, documentId, expiryBefore, historyBefore,
            statusAuditsBefore, invoiceAuditsBefore);

        // The pre-existing invoice is untouched and still the only one.
        var invoice = Assert.Single(
            await InvoicesForTaskAsync(taskId));
        Assert.Equal(seededInvoiceId, invoice.Id);
        Assert.Equal("SFI-PRE-EXISTING-001", invoice.InvoiceNumber);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
        Assert.Null(invoice.PaidAt);

        // The task was never advanced, so the request is no longer refused
        // as "already completed" - it re-runs the completion and fails on the
        // same guard, which is the point: the failure is visible and the task
        // is not silently half-done.
        var retry = await CompleteAsync(taskId);
        var retryBody = await retry.Content.ReadAsStringAsync();

        AssertConflict(retry, "retry");
        Assert.DoesNotContain("already been completed", retryBody);
        Assert.Contains("already exists", retryBody);

        await AssertNothingCommittedAsync(
            taskId, documentId, expiryBefore, historyBefore,
            statusAuditsBefore, invoiceAuditsBefore);
    }

    // ==================================================================
    // 5-2: the generated invoice number is already taken
    // ==================================================================

    /// <summary>
    /// The event handler derives the number as
    /// "SFI-{yyyyMMdd}-{first 8 chars of the task id}". The uniqueness check
    /// spans all non-deleted invoices, so a number already taken by a
    /// DIFFERENT task blocks creation for this one.
    ///
    /// The expected number is derived the same way the handler derives it.
    /// Should the suite straddle midnight UTC the number simply will not
    /// collide and this test fails loudly rather than silently passing.
    /// </summary>
    [Fact(DisplayName =
        "D4-10.2: an invoice number collision rolls the completion back and the task keeps no invoice")]
    public async Task InvoiceNumberCollision_RollsBackCompletion()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        // A second task, so the colliding invoice belongs to someone else.
        var otherDocumentId = await SeedDocumentAsync();
        var otherTaskId = await SeedApprovedTaskAsync(otherDocumentId);

        var collidingNumber =
            $"SFI-{DateTime.UtcNow:yyyyMMdd}-" +
            $"{taskId.ToString()[..8].ToUpper()}";

        var collidingInvoiceId = await SeedInvoiceAsync(
            otherTaskId, collidingNumber, amount: 99.00m);

        var expiryBefore =
            await ReadDocumentExpiryAsync(documentId);
        var historyBefore = await HistoryCountAsync(taskId);
        var statusAuditsBefore =
            await CompletionAuditCountAsync(
                "RenewalTaskStatusUpdated", taskId);
        var invoiceAuditsBefore =
            await CompletionAuditCountAsync(
                "ServiceFeeInvoiceCreated", taskId);

        var response = await CompleteAsync(taskId);
        var body = await response.Content.ReadAsStringAsync();

        // The caller is told the completion failed...
        AssertConflict(response, "complete");
        Assert.Contains("invoice number", body);

        // ...and the completion left no trace.
        await AssertNothingCommittedAsync(
            taskId, documentId, expiryBefore, historyBefore,
            statusAuditsBefore, invoiceAuditsBefore);

        // The task owns no invoice whatsoever.
        Assert.Empty(await InvoicesForTaskAsync(taskId));

        // The other task's invoice is untouched, so the collision did not
        // corrupt anyone's billing state.
        var otherInvoice = Assert.Single(
            await InvoicesForTaskAsync(otherTaskId));

        Assert.Equal(collidingInvoiceId, otherInvoice.Id);
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, otherInvoice.Status);
        Assert.Equal(99.00m, otherInvoice.Amount);
    }

    // ==================================================================
    // 5-3: the failure clears and the completion can be retried
    // ==================================================================

    /// <summary>
    /// The property that was actually missing before D4. The same
    /// invoice-number collision blocks the first attempt and rolls the whole
    /// completion back; once the colliding invoice is soft-deleted the retry
    /// succeeds and produces the complete, correct result - one history row,
    /// one status audit, one document audit, one invoice and one invoice
    /// audit. Under the previous arrangement the retry was impossible.
    /// </summary>
    [Fact(DisplayName =
        "D4-10.3: once the blocking invoice is gone the retried completion succeeds and writes the full audit chain")]
    public async Task ClearedFailure_CompletionSucceedsOnRetry()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        var otherDocumentId = await SeedDocumentAsync();
        var otherTaskId = await SeedApprovedTaskAsync(otherDocumentId);

        var collidingNumber =
            $"SFI-{DateTime.UtcNow:yyyyMMdd}-" +
            $"{taskId.ToString()[..8].ToUpper()}";

        var collidingInvoiceId = await SeedInvoiceAsync(
            otherTaskId, collidingNumber, amount: 99.00m);

        // First attempt: blocked, and nothing is committed.
        var historyBaseline = await HistoryCountAsync(taskId);

        var failed = await CompleteAsync(taskId);
        AssertConflict(failed, "first attempt");

        Assert.Equal(
            RenewalTaskStatus.Approved,
            (await ReadTaskAsync(taskId)).Status);
        Assert.Empty(await InvoicesForTaskAsync(taskId));

        // The failed attempt added no history row.
        Assert.Equal(historyBaseline, await HistoryCountAsync(taskId));

        // Clear the blocker by voiding the colliding invoice. Soft-deleted
        // rows are excluded from the uniqueness checks by design, which is the
        // same mechanism the admin repair path relies on.
        await WithScopeDbContextAsync(db =>
        {
            var invoice = db.ServiceFeeInvoices
                .Single(x => x.Id == collidingInvoiceId);

            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            db.SaveChanges();
        });

        // Retry: the task was never advanced, so this runs the completion
        // for real.
        var retry = await CompleteAsync(taskId);
        Assert.Equal(
            HttpStatusCode.OK, retry.StatusCode);

        // The task is now terminal, with the full write set committed once.
        var task = await ReadTaskAsync(taskId);
        Assert.Equal(RenewalTaskStatus.Updated, task.Status);
        Assert.NotNull(task.CompletedAt);

        Assert.Equal(
            new DateTime(2030, 9, 30),
            (await ReadDocumentExpiryAsync(documentId)).Date);

        // Exactly one invoice, created for this task.
        var invoice = Assert.Single(
            await InvoicesForTaskAsync(taskId));
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Equal(ServiceFee, invoice.Amount);

        var created = Assert.Single(
            await InvoicesForTaskAsync(taskId));
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, created.Status);
        Assert.Null(created.PaidAt);
        Assert.Equal(ServiceFee, created.Amount);

        // The invoice is Pending, so it was never paid and never had a
        // settlement timestamp.
        Assert.Equal(
            historyBaseline + 1,
            await HistoryCountAsync(taskId));

        // And exactly one of each audit row.
        Assert.Equal(
            1,
            await CompletionAuditCountAsync(
                "RenewalTaskStatusUpdated", taskId));
        Assert.Equal(
            1,
            await CompletionAuditCountAsync(
                "DocumentRenewed", documentId));
        Assert.Equal(
            1,
            await InvoiceCreatedAuditCountForTaskAsync(taskId));
    }
}
