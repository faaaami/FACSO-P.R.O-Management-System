using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Jobs;
using PROManagementSystem.Application.Common.Services;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// P9.2: the bounded overdue recovery sweep.
///
/// The gap this covers is real and structural, not a missing feature.
/// DocumentExpiryJob calls GetExpiringAsync with includeExpired: false,
/// whose window lower bound is expiry_date &gt;= CURRENT_TIMESTAMP, so an
/// already-expired document can never be a candidate. Flipping that to
/// includeExpired: true would not fix it either — that drops the lower bound
/// rather than moving it, so the scanner would re-raise every expired
/// document on record on every run, forever.
///
/// So recovery is bounded to the 30 days immediately after expiry: inside the
/// window the document gets the same renewal task the proactive scanner would
/// have raised, and the window then closes behind it. A document that expired
/// more than 30 days ago is left alone.
///
/// These tests drive the real job against real PostgreSQL and assert on the
/// stored rows rather than on logs or counters, because the counters are an
/// implementation detail and the rows are the contract. Every assertion is
/// scoped to a document id this test created — the shared seed plants its own
/// documents inside the forward 90-day window, so absolute counts across the
/// table are never meaningful here.
///
/// Two rules the implementation must not break, both asserted below:
///
///   - Recurring renewal stays possible. A previous cycle's Updated task does
///     not occupy the "one live task" slot, so it must not suppress a new
///     cycle (test 9). Any rule that skipped a document merely because some
///     task for it was ever completed would silently end renewal forever.
///   - Bounded recovery stays bounded. A document past the window is not
///     re-raised (test 2). Recovery that never stops is just noise.
///
/// The 30-day edges are never seeded exactly. There is no clock abstraction in
/// this codebase and CURRENT_TIMESTAMP advances while the test runs, so an
/// exact -30/-29 boundary would be flaky. The convention already used by
/// DocumentExpiryWindowTests applies: probe with a margin on each side.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentOverdueSweepTests : IntegrationTestBase
{
    private const int OverdueWindowDays = 30;
    private const int PageSize = 100;

    private static readonly Guid EmployeeA =
        TestDataSeeder.Ids.EmployeeA;

    private static readonly Guid AgentA =
        TestDataSeeder.Ids.AgentA;

    private static readonly Guid CompanyA =
        TestDataSeeder.Ids.CompanyA;

    /// <summary>Matches DatabaseFixture's RenewalAutomation:UserId.</summary>
    private static readonly Guid AutomationUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    public DocumentOverdueSweepTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static DateTime InDays(int days)
        => DateTime.UtcNow.AddDays(days);

    private static string DocumentNumberFor(Guid id)
        => $"DOC-OVD-{id.ToString("N")[..8]}";

    /// <summary>
    /// Plants a document owned by an employee of company A, so
    /// RenewalTaskCreationService can resolve a company for it. Default
    /// isActive = true because the sweep filters inactive documents in SQL.
    /// </summary>
    private Task SeedDocumentAsync(
        Guid id,
        DateTime? expiryDate,
        bool isDeleted = false,
        bool isActive = true)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = id,
                EmployeeId = EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber = DocumentNumberFor(id),
                IssueDate = now.AddYears(-1),
                ExpiryDate = expiryDate,
                FileUrl = $"https://storage.test/{id:N}",
                FileName = "overdue.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now : null
            });

            db.SaveChanges();
        });

    /// <summary>
    /// Runs the sweep against a real database, with the renewal-task service
    /// wrapped so that it refuses exactly one document and delegates every
    /// other document to the real implementation.
    ///
    /// The refusal is substituted rather than seeded, and that is a finding
    /// rather than a shortcut. No document row can actually make
    /// RenewalTaskCreationService throw, because every rejection it performs is
    /// either impossible or already excluded upstream:
    ///
    ///   - "not found" is unreachable, the id came straight off the query.
    ///   - deleted and inactive documents are filtered out by the sweep query
    ///     itself, before the service is called.
    ///   - an unresolvable company cannot be constructed. The documents table
    ///     enforces CK_documents_exactly_one_owner, so an owner id is always
    ///     present; the owner's company id is a non-nullable column with a
    ///     foreign key, so it always resolves; and the service reads the
    ///     document with IgnoreQueryFilters(), which lifts the soft-delete
    ///     filter on the included Employee and ClientEntity, so even a
    ///     soft-deleted owner still resolves. A document owned by a
    ///     soft-deleted client entity was tried first and still produced a
    ///     task.
    ///
    /// So the per-document try/catch in the job is a genuine guard whose
    /// trigger is not reproducible through data. This is the one test that
    /// substitutes a collaborator, because the behaviour under test is the
    /// job's isolation of that catch, not the service's validation.
    /// </summary>
    private async Task RunSweepRejectingAsync(Guid rejectedDocumentId)
    {
        using var factory = Factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(
                services => services.AddScoped<IRenewalTaskCreationService>(
                    sp => new RejectingRenewalTaskCreationService(
                        new RenewalTaskCreationService(
                            sp.GetRequiredService<IApplicationDbContext>()),
                        rejectedDocumentId))));

        using var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();

        var job = scope.ServiceProvider
            .GetRequiredService<DocumentOverdueSweepJob>();

        await job.ExecuteAsync();
    }

    /// <summary>
    /// Refuses one document id and otherwise defers to the real service, so
    /// every document that does get a task still gets a real one, written by
    /// production code, with its real history and audit rows.
    /// </summary>
    private sealed class RejectingRenewalTaskCreationService(
        IRenewalTaskCreationService inner,
        Guid rejectedDocumentId) : IRenewalTaskCreationService
    {
        public Task<Guid?> CreateAsync(
            Guid documentId,
            Guid changedBy,
            CancellationToken cancellationToken)
            => documentId == rejectedDocumentId
                ? throw new InvalidOperationException(
                    "Simulated refusal for a single document.")
                : inner.CreateAsync(
                    documentId,
                    changedBy,
                    cancellationToken);
    }

    /// <summary>
    /// Pre-existing task in a given state. The sweep never looks at these,
    /// so they exist purely to prove which states do and do not suppress a
    /// new one.
    /// </summary>
    private Task SeedExistingTaskAsync(
        Guid documentId,
        RenewalTaskStatus status,
        Guid? assignedStaffId = null)
        => WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.RenewalTasks.Add(new RenewalTask
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ClientCompanyId = CompanyA,
                AssignedStaffId = assignedStaffId,
                Status = status,
                BlockedReason = status == RenewalTaskStatus.Blocked
                    ? "Awaiting client document"
                    : null,
                BlockedSince = status == RenewalTaskStatus.Blocked
                    ? now
                    : null,
                CompletedAt =
                    status == RenewalTaskStatus.Updated ? now : null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

    private Task SeedAutomationUserAsync()
        => WithScopeDbContextAsync(db =>
        {
            if (db.Users.Any(u => u.Id == AutomationUserId))
            {
                return;
            }

            var now = DateTime.UtcNow;

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
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            });

            db.SaveChanges();
        });

    private async Task RunSweepAsync()
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var job = scope.ServiceProvider
            .GetRequiredService<DocumentOverdueSweepJob>();

        await job.ExecuteAsync();
    }

    private async Task RunProactiveJobAsync()
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var job = scope.ServiceProvider
            .GetRequiredService<DocumentExpiryJob>();

        await job.ExecuteAsync();
    }

    /// <summary>
    /// Live tasks: exactly the set the filtered unique index governs
    /// (not deleted, status != Updated).
    /// </summary>
    private async Task<int> LiveTaskCountAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
            count = db.RenewalTasks.Count(x =>
                x.DocumentId == documentId &&
                !x.IsDeleted &&
                x.Status != RenewalTaskStatus.Updated));

        return count;
    }

    /// <summary>
    /// Every row including soft-deleted and completed ones. Uses
    /// IgnoreQueryFilters because the global filter would otherwise hide
    /// exactly the rows a few of these tests are about.
    /// </summary>
    private async Task<int> AllTaskRowsAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
            count = db.RenewalTasks
                .IgnoreQueryFilters()
                .Count(x => x.DocumentId == documentId));

        return count;
    }

    private async Task<RenewalTask?> TaskForDocumentAsync(
        Guid documentId)
    {
        RenewalTask? task = null;

        await WithScopeDbContextAsync(db =>
            task = db.RenewalTasks
                .IgnoreQueryFilters()
                .FirstOrDefault(x => x.DocumentId == documentId));

        return task;
    }

    private Task MoveExpiryAsync(
        Guid documentId,
        DateTime expiryDate)
        => WithScopeDbContextAsync(db =>
        {
            var document = db.Documents.Single(
                x => x.Id == documentId);

            document.ExpiryDate = expiryDate;
            document.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

    private async Task<int> NotificationCountAsync()
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
            count = db.Notifications.Count());

        return count;
    }

    // ==================================================================
    // 1-5  Window and eligibility
    // ==================================================================

    [Fact(DisplayName =
        "P9.2-1. A document expired 5 days ago is inside the recovery window " +
        "and gets exactly one task")]
    public async Task OverdueInsideWindow_GetsExactlyOneTask()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));

        await RunSweepAsync();

        Assert.Equal(1, await LiveTaskCountAsync(id));
        Assert.Equal(1, await AllTaskRowsAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-2. A document expired 31 days ago is outside the window and " +
        "gets no task — recovery is bounded")]
    public async Task OlderThanWindow_IsNotRecovered()
    {
        await SeedAutomationUserAsync();

        // 31 days: just outside the 30-day window, so a 90-day window would
        // have picked it up.
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-31));

        // 89 days past expiry: outside recovery even though it sits comfortably
        // inside the proactive 90-day span. This is the assertion that the
        // sweep did not simply reuse the proactive window, and did not take
        // includeExpired: true either — that variant drops the lower bound
        // instead of moving it, and would have raised a task for this one.
        var ancientId = Guid.NewGuid();
        await SeedDocumentAsync(ancientId, InDays(-89));

        await RunSweepAsync();

        Assert.Equal(0, await AllTaskRowsAsync(id));
        Assert.Equal(0, await AllTaskRowsAsync(ancientId));
    }

    [Fact(DisplayName =
        "P9.2-3. A document that has not expired yet is not an overdue " +
        "candidate — the proactive window owns it")]
    public async Task FutureDocument_IsNotAnOverdueCandidate()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(5));

        await RunSweepAsync();

        Assert.Equal(0, await AllTaskRowsAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-4. A document with no expiry date is not an overdue candidate")]
    public async Task NullExpiry_IsNotAnOverdueCandidate()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, expiryDate: null);

        await RunSweepAsync();

        Assert.Equal(0, await AllTaskRowsAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-5. Soft-deleted and inactive expired documents are ignored, " +
        "even though their expiry dates are inside the window")]
    public async Task SoftDeletedOrInactiveDocument_IsIgnored()
    {
        await SeedAutomationUserAsync();

        var deletedId = Guid.NewGuid();
        await SeedDocumentAsync(deletedId, InDays(-5), isDeleted: true);

        var inactiveId = Guid.NewGuid();
        await SeedDocumentAsync(inactiveId, InDays(-5), isActive: false);

        await RunSweepAsync();

        Assert.Equal(0, await AllTaskRowsAsync(deletedId));

        // The inactive one is excluded in SQL, so the creation service never
        // sees it and never throws on it. That is the point of the is_active
        // predicate: this query and the proactive scanner both exclude
        // inactive documents in SQL, so neither hands one to the service.
        Assert.Equal(0, await AllTaskRowsAsync(inactiveId));
    }

    // ==================================================================
    // 6-9  Deduplication, and the Updated rule that must not harden
    // ==================================================================

    [Fact(DisplayName =
        "P9.2-6. An existing Submitted task suppresses a duplicate")]
    public async Task ExistingSubmittedTask_PreventsDuplicate()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));
        await SeedExistingTaskAsync(id, RenewalTaskStatus.Submitted);

        await RunSweepAsync();

        Assert.Equal(1, await AllTaskRowsAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-7. An existing assigned, in-flight task suppresses a duplicate")]
    public async Task ExistingAssignedTask_PreventsDuplicate()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));
        await SeedExistingTaskAsync(
            id,
            RenewalTaskStatus.Approved,
            AgentA);

        await RunSweepAsync();

        Assert.Equal(1, await AllTaskRowsAsync(id));
        Assert.Equal(1, await LiveTaskCountAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-8. An existing Blocked task suppresses a duplicate")]
    public async Task ExistingBlockedTask_PreventsDuplicate()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));
        await SeedExistingTaskAsync(id, RenewalTaskStatus.Blocked);

        await RunSweepAsync();

        // Blocked != Updated, so a blocked task still occupies the one live
        // slot. The sweep must not open a second workflow for a document an
        // admin has already parked.
        Assert.Equal(1, await AllTaskRowsAsync(id));
        Assert.Equal(1, await LiveTaskCountAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-9. A previous cycle's Updated task does NOT suppress a new " +
        "cycle")]
    public async Task PreviousUpdatedTask_AllowsANewCycle()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));
        await SeedExistingTaskAsync(id, RenewalTaskStatus.Updated);

        Assert.Equal(0, await LiveTaskCountAsync(id));

        await RunSweepAsync();

        // The Updated row is outside the unique index's filter, so it never
        // occupied the live slot and a new task is the correct outcome. If
        // this ever fails, the likely cause is a "already processed" check on
        // Updated — which would end recurring renewal permanently, because the
        // renewed document's next expiry would always find that same row.
        Assert.Equal(2, await AllTaskRowsAsync(id));
        Assert.Equal(1, await LiveTaskCountAsync(id));
    }

    // ==================================================================
    // 10-11  Leaving the window, and not colliding with the proactive job
    // ==================================================================

    [Fact(DisplayName =
        "P9.2-10. Renewing a document out of the window stops it being " +
        "recovered on later runs")]
    public async Task RenewedDocument_LeavesTheOverdueSet()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));

        await RunSweepAsync();
        Assert.Equal(1, await LiveTaskCountAsync(id));

        // The renewal itself: the new document gets a future expiry.
        await MoveExpiryAsync(id, InDays(365));

        await RunSweepAsync();
        await RunSweepAsync();

        // Still exactly the one task. Repeated runs must not pile up work for
        // a document that has been renewed.
        Assert.Equal(1, await AllTaskRowsAsync(id));
    }

    [Fact(DisplayName =
        "P9.2-11. A proactive task that outlives its document's expiry " +
        "suppresses the overdue sweep")]
    public async Task ProactiveTask_ThenDocumentExpires_NoDuplicate()
    {
        await SeedAutomationUserAsync();

        // The normal lifecycle: the document is inside the forward 90-day
        // window, so the proactive scanner raises a task for it. A document
        // cannot be in both windows at once — proactive is expiry >= now and
        // overdue is expiry < now — so rather than invent an expiry value that
        // satisfies both, this moves the document across the boundary the way
        // time does, with the proactive task still live. That is the real
        // sequence, and it is the one that could double up work.
        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(10));

        await RunProactiveJobAsync();
        Assert.Equal(1, await LiveTaskCountAsync(id));

        // Time passes and the document expires with its task still open.
        await MoveExpiryAsync(id, InDays(-5));

        await RunSweepAsync();

        // The live task from the proactive cycle is exactly what stops the
        // sweep. One task, not two.
        Assert.Equal(1, await AllTaskRowsAsync(id));
        Assert.Equal(1, await LiveTaskCountAsync(id));
    }

    // ==================================================================
    // 12-13  Overlap and failure isolation
    // ==================================================================

    [Fact(DisplayName =
        "P9.2-12. Two overlapping sweeps still produce one task, one history " +
        "row and one audit row")]
    public async Task OverlappingSweeps_ProduceOneTaskOneHistoryOneAudit()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));

        // Two independent scopes, so two independent ApplicationDbContexts
        // and a genuinely concurrent read-then-insert race rather than two
        // runs serialised behind one context. Neither sweep is protected by a
        // Hangfire concurrency filter — the filtered unique index plus the
        // creation service's existing 23505 absorption is the whole defence.
        await using var scope1 =
            Factory.Services.CreateAsyncScope();
        await using var scope2 =
            Factory.Services.CreateAsyncScope();

        var job1 = scope1.ServiceProvider
            .GetRequiredService<DocumentOverdueSweepJob>();
        var job2 = scope2.ServiceProvider
            .GetRequiredService<DocumentOverdueSweepJob>();

        await Task.WhenAll(
            job1.ExecuteAsync(),
            job2.ExecuteAsync());

        var task = await TaskForDocumentAsync(id);

        Assert.NotNull(task);
        Assert.Equal(1, await LiveTaskCountAsync(id));
        Assert.Equal(1, await AllTaskRowsAsync(id));

        await WithScopeDbContextAsync(db =>
        {
            // The loser's task, history and audit were added to one change
            // tracker and written by one SaveChanges, so the unique-index
            // failure rolls all three back together. Nothing partial survives.
            Assert.Equal(
                1,
                db.RenewalTaskHistories.Count(x =>
                    x.RenewalTaskId == task!.Id));

            Assert.Equal(
                1,
                db.AuditLogs.Count(x =>
                    x.EntityType == "RenewalTask" &&
                    x.EntityId == task!.Id &&
                    x.Action == "RenewalTaskCreated"));
        });
    }

    [Fact(DisplayName =
        "P9.2-13. One document the service refuses does not strand the " +
        "others in the run")]
    public async Task OneRejectedDocument_DoesNotStopTheRun()
    {
        await SeedAutomationUserAsync();

        // Two ordinary candidates. The rejected one is refused by a wrapped
        // service rather than by seeded data, because no seeded document can
        // actually make the real service refuse one — see
        // RunSweepRejectingAsync for the full reasoning.
        var rejectedId = Guid.NewGuid();
        await SeedDocumentAsync(rejectedId, InDays(-20));

        var validId = Guid.NewGuid();
        await SeedDocumentAsync(validId, InDays(-5));

        await RunSweepRejectingAsync(rejectedId);

        // The refusal is contained, not merely tolerated: the run completes,
        // the refused document gets nothing, and the other candidate is fully
        // processed by the real service — task, history and audit row.
        Assert.Equal(0, await AllTaskRowsAsync(rejectedId));
        Assert.Equal(1, await LiveTaskCountAsync(validId));

        var task = await TaskForDocumentAsync(validId);

        Assert.NotNull(task);

        await WithScopeDbContextAsync(db =>
        {
            Assert.Equal(
                1,
                db.RenewalTaskHistories.Count(x =>
                    x.RenewalTaskId == task!.Id));

            Assert.Equal(
                1,
                db.AuditLogs.Count(x =>
                    x.EntityId == task!.Id &&
                    x.Action == "RenewalTaskCreated"));
        });
    }

    // ==================================================================
    // 14-15  Side effects
    // ==================================================================

    [Fact(DisplayName =
        "P9.2-14. A swept task carries the existing automatic-creation " +
        "history and audit trail")]
    public async Task SweptTask_HasCorrectHistoryAndAudit()
    {
        await SeedAutomationUserAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));

        await RunSweepAsync();

        var task = await TaskForDocumentAsync(id);

        Assert.NotNull(task);

        // Unassigned and Submitted: the sweep creates the same task shape the
        // proactive scanner does, and assignment stays an explicit Admin act.
        Assert.Equal(RenewalTaskStatus.Submitted, task!.Status);
        Assert.Null(task.AssignedStaffId);
        Assert.Equal(CompanyA, task.ClientCompanyId);
        Assert.False(task.IsDeleted);

        await WithScopeDbContextAsync(db =>
        {
            var history =
                db.RenewalTaskHistories.Single(
                    x => x.RenewalTaskId == task.Id);

            Assert.Equal(
                RenewalTaskStatus.Submitted, history.Status);

            Assert.Equal(
                AutomationUserId, history.ChangedBy);

            // The existing service wording, unchanged.
            Assert.Equal(
                "Renewal task created automatically.",
                history.Note);

            var audit =
                db.AuditLogs.Single(x =>
                    x.EntityType == "RenewalTask" &&
                    x.EntityId == task.Id);

            Assert.Equal("RenewalTaskCreated", audit.Action);
            Assert.Equal(AutomationUserId, audit.UserId);

            // The job path correlates on the document id in the description
            // (the API create path uses the document number instead), so this
            // is what proves the audit belongs to this document.
            Assert.Contains(id.ToString(), audit.Description!);
        });
    }

    [Fact(DisplayName =
        "P9.2-15. The sweep creates no notifications")]
    public async Task Sweep_CreatesNoNotifications()
    {
        await SeedAutomationUserAsync();

        var before = await NotificationCountAsync();

        var id = Guid.NewGuid();
        await SeedDocumentAsync(id, InDays(-5));

        await RunSweepAsync();

        Assert.Equal(1, await LiveTaskCountAsync(id));

        // Notifications are deliberately not part of this sweep: the model has
        // no type or dedupe key, so a daily sweep would write one row per
        // admin per document and repeat it every day for as long as the
        // document stayed overdue. Admin discovers these tasks through the
        // existing task list, dashboard count and audit feed instead. This
        // assertion pins that decision, so a later change has to be deliberate.
        Assert.Equal(before, await NotificationCountAsync());
    }
}
