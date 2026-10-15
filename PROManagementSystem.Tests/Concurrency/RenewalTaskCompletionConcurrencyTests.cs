using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Concurrency;

/// <summary>
/// D4-9: concurrent completion of one renewal task.
///
/// The defect this covers is the reachable one. Before D4 the handler read
/// the task with no lock and committed the status change before invoice
/// creation, so two simultaneous completion requests (a double-clicked Save,
/// or an Agent and an Admin acting at once) could both read Approved, both
/// pass the terminal check, and both commit. The result was a duplicate
/// history row, a duplicate status audit, and one of the two requests failing
/// on the invoice's own uniqueness guards - surfacing to the user as the
/// confusing "A service fee invoice already exists for this renewal task".
///
/// D4 takes a FOR UPDATE row lock on the task before the read that decides
/// whether completion is allowed, and puts the whole completion in one
/// transaction. The loser now waits, re-reads the committed Updated state and
/// exits on the ordinary terminal conflict, so exactly one completion is
/// recorded and the loser is told the task is already completed.
///
/// Every count below is correlated to this test's own task, document and
/// invoice IDs. No assertion uses a global table count, because the test
/// database also holds the shared seeded rows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RenewalTaskCompletionConcurrencyTests
    : IntegrationTestBase
{
    private const decimal ServiceFee = 2500.00m;

    public RenewalTaskCompletionConcurrencyTests(
        DatabaseFixture fixture)
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
                    $"DOC-COMPCOMP-{documentId.ToString("N")[..8]}",
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

    /// <summary>
    /// The admin route, which issues the same command. Racing the assigned
    /// Agent against an Admin is the realistic double-completion: the Admin
    /// bypasses the ownership guard, so before D4 both requests were able to
    /// act on the same Approved task.
    /// </summary>
    private Task<HttpResponseMessage> CompleteAsAdminAsync(
        Guid taskId)
        => ScopeClient.SendAsync(ScopePatch(
            $"/api/v1/admin/tasks/{taskId}/status",
            new
            {
                status = "Updated",
                renewedExpiryDate = "2030-09-30"
            },
            TestDataSeeder.Ids.Admin,
            "Admin"));

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
                InvoiceDate = DateOnly.FromDateTime(now),
                DueDate = DateOnly.FromDateTime(now.AddDays(30)),
                Amount = amount,
                Currency = "AED",
                Status = ServiceFeeInvoiceStatus.Pending,
                Description = "Collision staged for the completion seam.",
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
                .CountAsync(x =>
                    x.RenewalTaskId == taskId &&
                    x.Status == RenewalTaskStatus.Updated);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<int> CountAuditAsync(
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
    /// Counts the completion notification written by
    /// ClientNotificationOnRenewalTaskCompletedHandler. Matched on its message
    /// marker rather than on the task id alone, because the agent's
    /// "TaskStatusChanged" notification also carries the task id and a
    /// successful completion legitimately produces one of each.
    /// </summary>
    private async Task<int> CompletionNotificationCountAsync(
        Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.Notifications
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.Message != null &&
                    x.Message.StartsWith("RenewalTaskCompleted") &&
                    x.Message.Contains(taskId.ToString()));
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private static void AssertOk(
        HttpResponseMessage response,
        string stage)
        => Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{stage}: expected 200 but got " +
            $"{(int)response.StatusCode}.");

    // ==================================================================
    // 1. Two concurrent completions
    // ==================================================================

    [Fact(DisplayName =
        "D4-9.1: two concurrent completions of the same task settle once - one 200, one terminal conflict, one invoice")]
    public async Task TwoConcurrentCompletions_SettleOnce()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        var expiryBefore =
            await ReadDocumentExpiryAsync(documentId);

        // No sleeps: both requests are released together and the row lock in
        // the handler is what decides the winner.
        var responses = await Task.WhenAll(
            Task.Run(() => CompleteAsAgentAsync(taskId)),
            Task.Run(() => CompleteAsAdminAsync(taskId)));

        var successes = responses
            .Where(r => r.StatusCode == HttpStatusCode.OK)
            .ToList();
        var conflicts = responses
            .Where(r => r.StatusCode == HttpStatusCode.Conflict)
            .ToList();

        Assert.Single(successes);
        Assert.Single(conflicts);

        // The loser is told the task is already completed. Before D4 the loser
        // instead failed on the invoice duplicate guard, which reported a
        // billing collision for a request the user had every right to make.
        Assert.Contains(
            "already been completed",
            await conflicts[0].Content.ReadAsStringAsync());

        // The task is terminal exactly once.
        var task = await ReadTaskAsync(taskId);
        Assert.Equal(RenewalTaskStatus.Updated, task.Status);
        Assert.NotNull(task.CompletedAt);

        // The document was renewed once.
        Assert.Equal(
            new DateTime(2030, 9, 30),
            (await ReadDocumentExpiryAsync(documentId)).Date);
        Assert.NotEqual(expiryBefore, task.CompletedAt.Value);

        // Exactly one invoice for this task.
        var invoice = Assert.Single(
            await InvoicesForTaskAsync(taskId));
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, invoice.Status);
        Assert.Equal(ServiceFee, invoice.Amount);

        // Exactly one completion history row.
        Assert.Equal(1, await HistoryCountAsync(taskId));

        // Exactly one audit row of each kind that completion writes.
        Assert.Equal(
            1,
            await CountAuditAsync(
                "RenewalTaskStatusUpdated", taskId));
        Assert.Equal(
            1,
            await CountAuditAsync("DocumentRenewed", documentId));
        Assert.Equal(
            1,
            await CountAuditAsync(
                "ServiceFeeInvoiceCreated", invoice.Id));

        // And the client notification was written once, not twice.
        Assert.Equal(1, await CompletionNotificationCountAsync(taskId));
    }

    // ==================================================================
    // 2. A failing completion rolls back and can be retried
    // ==================================================================

    /// <summary>
    /// Both racing attempts are blocked by the same deterministic invoice
    /// number collision, so both must fail and neither may commit anything.
    /// Once the collision is cleared the retry completes normally, which is
    /// the property the pre-D4 handler could not offer: the first attempt
    /// left the task terminal, so the retry was refused as "already
    /// completed" and the invoice could never be created by replaying.
    /// </summary>
    [Fact(DisplayName =
        "D4-9.2: concurrent completions that fail leave the task Approved, and the completion succeeds once the failure clears")]
    public async Task FailedConcurrentCompletions_RollBackAndAllowRetry()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        // A colliding number owned by a different task blocks invoice
        // creation for this one.
        var otherDocumentId = await SeedDocumentAsync();
        var otherTaskId = await SeedApprovedTaskAsync(otherDocumentId);

        var collidingNumber =
            $"SFI-{DateTime.UtcNow:yyyyMMdd}-" +
            $"{taskId.ToString()[..8].ToUpper()}";

        var collidingInvoiceId = await SeedInvoiceAsync(
            otherTaskId, collidingNumber, amount: 99.00m);

        var expiryBefore =
            await ReadDocumentExpiryAsync(documentId);
        var historyBaseline = await HistoryCountAsync(taskId);

        var failures = await Task.WhenAll(
            Task.Run(() => CompleteAsAgentAsync(taskId)),
            Task.Run(() => CompleteAsAgentAsync(taskId)));

        Assert.All(failures, r => Assert.True(
            r.StatusCode == HttpStatusCode.Conflict,
            $"Expected 409 but got {(int)r.StatusCode}."));

        foreach (var failure in failures)
        {
            Assert.Contains(
                "invoice number",
                await failure.Content.ReadAsStringAsync());
        }

        // Nothing from either attempt survived.
        var task = await ReadTaskAsync(taskId);
        Assert.Equal(RenewalTaskStatus.Approved, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Equal(
            expiryBefore,
            await ReadDocumentExpiryAsync(documentId));
        Assert.Equal(historyBaseline, await HistoryCountAsync(taskId));
        Assert.Equal(
            0,
            await CountAuditAsync(
                "RenewalTaskStatusUpdated", taskId));
        Assert.Equal(
            0,
            await CountAuditAsync("DocumentRenewed", documentId));
        Assert.Empty(await InvoicesForTaskAsync(taskId));
        Assert.Equal(0, await CompletionNotificationCountAsync(taskId));

        // Clear the blocker, then retry.
        await WithScopeDbContextAsync(db =>
        {
            var invoice = db.ServiceFeeInvoices
                .Single(x => x.Id == collidingInvoiceId);

            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            db.SaveChanges();
        });

        AssertOk(
            await CompleteAsAgentAsync(taskId), "retry");

        var completed = await ReadTaskAsync(taskId);
        Assert.Equal(RenewalTaskStatus.Updated, completed.Status);
        Assert.NotNull(completed.CompletedAt);

        var created = Assert.Single(
            await InvoicesForTaskAsync(taskId));
        Assert.Equal(
            ServiceFeeInvoiceStatus.Pending, created.Status);

        Assert.Equal(1, await HistoryCountAsync(taskId));
        Assert.Equal(
            1,
            await CountAuditAsync(
                "RenewalTaskStatusUpdated", taskId));
        Assert.Equal(
            1,
            await CountAuditAsync("DocumentRenewed", documentId));
        Assert.Equal(
            1,
            await CountAuditAsync(
                "ServiceFeeInvoiceCreated", created.Id));
    }

    // ==================================================================
    // 3. Three concurrent completions
    // ==================================================================

    [Fact(DisplayName =
        "D4-9.3: three concurrent completions produce exactly one completion record and two terminal conflicts")]
    public async Task ThreeConcurrentCompletions_OneWins()
    {
        var documentId = await SeedDocumentAsync();
        var taskId = await SeedApprovedTaskAsync(documentId);

        var responses = await Task.WhenAll(
            Task.Run(() => CompleteAsAgentAsync(taskId)),
            Task.Run(() => CompleteAsAgentAsync(taskId)),
            Task.Run(() => CompleteAsAgentAsync(taskId)));

        Assert.Single(
            responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(
            2,
            responses.Count(
                r => r.StatusCode == HttpStatusCode.Conflict));

        // Both losers report the task as already completed, never an invoice
        // collision.
        foreach (var response in responses.Where(
            r => r.StatusCode == HttpStatusCode.Conflict))
        {
            Assert.Contains(
                "already been completed",
                await response.Content.ReadAsStringAsync());
        }

        var task = await ReadTaskAsync(taskId);
        Assert.Equal(RenewalTaskStatus.Updated, task.Status);

        // Still exactly one of everything.
        var invoice = Assert.Single(
            await InvoicesForTaskAsync(taskId));
        Assert.Equal(1, await HistoryCountAsync(taskId));
        Assert.Equal(
            1,
            await CountAuditAsync(
                "RenewalTaskStatusUpdated", taskId));
        Assert.Equal(
            1,
            await CountAuditAsync("DocumentRenewed", documentId));
        Assert.Equal(
            1,
            await CountAuditAsync(
                "ServiceFeeInvoiceCreated", invoice.Id));
        Assert.Equal(1, await CompletionNotificationCountAsync(taskId));
    }
}
