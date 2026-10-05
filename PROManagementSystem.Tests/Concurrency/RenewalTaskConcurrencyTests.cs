using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Application.Common.Services;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Concurrency;

/// <summary>
/// Hi-6: Renewal task creation concurrency.  The unique filtered index on
/// DocumentId (WHERE IsDeleted = false AND Status != 6) is the database-level
/// backstop.  These tests verify that concurrent job executions produce at
/// most one active task per document and that the losing race is treated as
/// an idempotent no-op.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RenewalTaskConcurrencyTests : IntegrationTestBase
{
    private static readonly Guid AutomationUserId =
        Guid.Parse(
            "00000000-0000-0000-0000-0000000000aa");

    public RenewalTaskConcurrencyTests(DatabaseFixture fixture)
        : base(fixture) { }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task SeedAutomationUserAsync()
    {
        await WithScopeDbContextAsync(db =>
        {
            if (!db.Users.Any(u => u.Id == AutomationUserId))
            {
                db.Users.Add(new User
                {
                    Id = AutomationUserId,
                    FullName = "Renewal Automation",
                    Email = "automation@test.local",
                    PasswordHash = "not-used",
                    Role = UserRole.Admin,
                    IsActive = true,
                    IsApproved = true,
                    EmailVerified = true,
                    ClientCompanyId = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    DeletedAt = null
                });
                db.SaveChanges();
            }
        });
    }

    private async Task SeedDocumentAsync(
        Guid documentId,
        Guid employeeId,
        bool isActive = true)
    {
        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = employeeId,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber = $"DOC-CONCUR-{documentId.ToString("N")[..8]}",
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(30),
                FileUrl = $"https://storage.test/{documentId}",
                FileName = "doc.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });
    }

    private async Task<int> ActiveTaskCountAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTasks.Count(x =>
                x.DocumentId == documentId &&
                !x.IsDeleted &&
                x.Status != RenewalTaskStatus.Updated);
        });

        return count;
    }

    private async Task<int> TotalTaskCountAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTasks.Count(x =>
                x.DocumentId == documentId &&
                !x.IsDeleted);
        });

        return count;
    }

    private async Task<int> AuditCountAsync(string action)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.AuditLogs.Count(x =>
                x.Action == action);
        });

        return count;
    }

    /// <summary>
    /// Counts the "RenewalTaskCreated" audits attributable to one specific
    /// document. The audit correlates on the document id in its Description
    /// (the EntityId is the task, not the document), and running the expiry
    /// job also creates tasks for whatever in-window documents the shared
    /// seed planted — so a global count delta is not meaningful here.
    /// </summary>
    private async Task<int> CreatedAuditCountForDocumentAsync(
        Guid documentId)
    {
        var needle = documentId.ToString();
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.AuditLogs.Count(x =>
                x.Action == "RenewalTaskCreated" &&
                x.Description != null &&
                x.Description.Contains(needle));
        });

        return count;
    }

    private RenewalTaskCreationService CreateService()
    {
        var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        return new RenewalTaskCreationService(context);
    }

    // ==================================================================
    // 1. Two concurrent creation attempts for the same document
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.1: two concurrent creations for the same document produce exactly one active task")]
    public async Task ConcurrentSameDocument_OneActiveTask()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        var results = await Task.WhenAll(
            Task.Run(async () =>
            {
                var svc = CreateService();
                return await svc.CreateAsync(
                    docId, AutomationUserId,
                    CancellationToken.None);
            }),
            Task.Run(async () =>
            {
                var svc = CreateService();
                return await svc.CreateAsync(
                    docId, AutomationUserId,
                    CancellationToken.None);
            }));

        // Exactly one returns a task ID, the other returns null.
        Assert.Single(results, r => r.HasValue);
        Assert.Single(results, r => !r.HasValue);

        // Only one active task in the database.
        Assert.Equal(1, await ActiveTaskCountAsync(docId));
    }

    // ==================================================================
    // 2. Repeated job execution — no duplicate
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.2: calling CreateAsync three times sequentially yields one active task")]
    public async Task RepeatedExecution_NoDuplicate()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        var svc = CreateService();

        var r1 = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);
        var r2 = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);
        var r3 = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);

        Assert.NotNull(r1);
        Assert.Null(r2);
        Assert.Null(r3);

        Assert.Equal(1, await ActiveTaskCountAsync(docId));
    }

    // ==================================================================
    // 3. Different documents — each gets its own task
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.3: different documents each receive their own active task")]
    public async Task DifferentDocuments_EachGetsTask()
    {
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();

        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docA, TestDataSeeder.Ids.EmployeeA);
        await SeedDocumentAsync(docB, TestDataSeeder.Ids.EmployeeB);

        var svc = CreateService();

        var idA = await svc.CreateAsync(
            docA, AutomationUserId, CancellationToken.None);
        var idB = await svc.CreateAsync(
            docB, AutomationUserId, CancellationToken.None);

        Assert.NotNull(idA);
        Assert.NotNull(idB);
        Assert.NotEqual(idA.Value, idB.Value);

        Assert.Equal(1, await ActiveTaskCountAsync(docA));
        Assert.Equal(1, await ActiveTaskCountAsync(docB));
    }

    // ==================================================================
    // 4. Existing active task — no duplicate
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.4: existing active task prevents new creation")]
    public async Task ExistingActiveTask_NoDuplicate()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        // Seed an existing active task.
        await WithScopeDbContextAsync(db =>
        {
            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Submitted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var svc = CreateService();
        var result = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, await ActiveTaskCountAsync(docId));
    }

    // ==================================================================
    // 5. Soft-deleted previous task — new active task allowed
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.5: soft-deleted previous task allows new active task")]
    public async Task SoftDeletedPrevious_AllowsNewTask()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        // Seed a soft-deleted task.
        await WithScopeDbContextAsync(db =>
        {
            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Submitted,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-10),
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow.AddDays(-5)
            });
            db.SaveChanges();
        });

        var svc = CreateService();
        var result = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, await ActiveTaskCountAsync(docId));
    }

    // ==================================================================
    // 6. Updated-status previous task — new active task allowed
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.6: Updated-status previous task allows new active task")]
    public async Task UpdatedPrevious_AllowsNewTask()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        // Seed a task with Updated status (treated as inactive).
        await WithScopeDbContextAsync(db =>
        {
            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Updated,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        var svc = CreateService();
        var result = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, await ActiveTaskCountAsync(docId));
    }

    // ==================================================================
    // 7. Unique-constraint violation handling — no partial side effects
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.7: unique-constraint violation leaves no partial history or audit")]
    public async Task UniqueViolation_NoPartialSideEffects()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        var beforeAudit = await AuditCountAsync("RenewalTaskCreated");

        var results = await Task.WhenAll(
            Task.Run(async () =>
            {
                var svc = CreateService();
                return await svc.CreateAsync(
                    docId, AutomationUserId,
                    CancellationToken.None);
            }),
            Task.Run(async () =>
            {
                var svc = CreateService();
                return await svc.CreateAsync(
                    docId, AutomationUserId,
                    CancellationToken.None);
            }));

        var afterAudit = await AuditCountAsync("RenewalTaskCreated");

        // Exactly one succeeded.
        Assert.Single(results, r => r.HasValue);

        // Only one audit log was created (no partial side effects).
        Assert.Equal(beforeAudit + 1, afterAudit);

        // Only one active task.
        Assert.Equal(1, await ActiveTaskCountAsync(docId));

        // Total tasks may be 1 (winner committed) or 2 if the loser's
        // history/audit were rolled back by the constraint violation.
        var total = await TotalTaskCountAsync(docId);
        Assert.True(total >= 1 && total <= 2);
    }

    // ==================================================================
    // 8. Hangfire job — repeated execution is safe
    // ==================================================================

    [Fact(DisplayName =
        "Hi-6.8: DocumentExpiryJob called twice yields one active task per document")]
    public async Task JobExecution_NoDuplicate()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        await using var scope1 = Factory.Services.CreateAsyncScope();
        var job1 = scope1.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();
        await job1.ExecuteAsync();

        await using var scope2 = Factory.Services.CreateAsyncScope();
        var job2 = scope2.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();
        await job2.ExecuteAsync();

        Assert.Equal(1, await ActiveTaskCountAsync(docId));
    }

    // ==================================================================
    // 9. D3 - genuinely concurrent job executions
    // ==================================================================

    /// <summary>
    /// Hi-6.8 above runs the two jobs one after the other, so it only proves
    /// sequential idempotence. Hangfire registers this job without
    /// [DisableConcurrentExecution], so two runs can genuinely overlap. Each
    /// job is resolved from its own DI scope, which gives it its own
    /// ApplicationDbContext, so the check-then-insert race really is
    /// concurrent rather than serialised behind a shared context.
    /// </summary>
    [Fact(DisplayName =
        "D3-Hi-6.9: two DocumentExpiryJob instances running concurrently yield one active task and one audit")]
    public async Task JobExecution_Concurrent_OneActiveTaskOneAudit()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        Assert.Equal(0, await CreatedAuditCountForDocumentAsync(docId));

        await using var scope1 = Factory.Services.CreateAsyncScope();
        await using var scope2 = Factory.Services.CreateAsyncScope();

        var job1 = scope1.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();
        var job2 = scope2.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();

        await Task.WhenAll(
            job1.ExecuteAsync(),
            job2.ExecuteAsync());

        // One winner; the loser is swallowed as an idempotent no-op.
        Assert.Equal(1, await ActiveTaskCountAsync(docId));

        // Exactly one creation audit attributable to this document, so the
        // losing race wrote no partial side effect.
        Assert.Equal(1, await CreatedAuditCountForDocumentAsync(docId));

        // The unique index is the backstop: at most the winner's row and a
        // rolled-back loser can exist.
        var total = await TotalTaskCountAsync(docId);
        Assert.True(total >= 1 && total <= 2);
    }

    // ==================================================================
    // 10. D3 - an existing Blocked task is still "active"
    // ==================================================================

    /// <summary>
    /// The filtered index excludes only Status != 6 (Updated). Blocked is 4,
    /// so a blocked task must still prevent a second active task. Hi-6.5
    /// covers soft-deleted and Hi-6.6 covers Updated, but nothing covered
    /// Blocked — the case most likely to regress if the filter is edited.
    /// </summary>
    [Fact(DisplayName =
        "D3-Hi-6.10: an existing Blocked task prevents a duplicate active task")]
    public async Task ExistingBlockedTask_PreventsNewTask()
    {
        var docId = Guid.NewGuid();
        await SeedAutomationUserAsync();
        await SeedDocumentAsync(docId, TestDataSeeder.Ids.EmployeeA);

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                AssignedStaffId = null,
                Status = RenewalTaskStatus.Blocked,
                BlockedReason = "Awaiting client documents",
                BlockedSince = now,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

        var svc = CreateService();
        var result = await svc.CreateAsync(
            docId, AutomationUserId, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, await ActiveTaskCountAsync(docId));
        Assert.Equal(1, await TotalTaskCountAsync(docId));
    }
}
