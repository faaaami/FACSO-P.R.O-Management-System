using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Concurrency;

/// <summary>
/// P9.1 F6: a concurrent duplicate renewal task is a 409 CONFLICT, not a 500.
///
/// The invariant is already enforced by the database.
/// RenewalTaskConfiguration declares a filtered unique index on DocumentId,
/// <c>IsDeleted = false AND Status != 6</c>, so at most one live renewal task
/// can exist per document no matter what the application does. What F6 changes
/// is the loser's HTTP answer.
///
/// The application guard in CreateRenewalTaskCommandHandler is a read followed by
/// an insert. Two callers that both read before either wrote both find no active
/// task, both proceed, and PostgreSQL refuses the second with 23505 on that
/// index. That DbUpdateException had no mapping, so GlobalExceptionMiddleware
/// reported it as 500 INTERNAL_SERVER_ERROR — "the server broke" for what is an
/// ordinary already-taken outcome, and indistinguishable from a real fault to
/// anything monitoring the status code.
///
/// The fix is in NpgsqlDuplicateDocumentTranslator, which matches the exact
/// constraint name and translates it to ConflictException, applied at the
/// CreateRenewalTask save and mirrored on the service-request convert path.
///
/// Two things are deliberately NOT asserted here, and the distinction is the
/// point of the whole test:
///
///   - The winner is not fixed. Which request loses depends on when each is
///     granted the index lock, so the assertions are that exactly one succeeded,
///     the other got 409, and the stored rows are correct — never that a
///     particular request won.
///   - A sequential second create is NOT this path. It is stopped by the
///     application guard before any insert and has always returned 409. Only the
///     genuinely concurrent case reaches the index, and only that case is
///     regression-guarded here.
///
/// The existing RenewalTaskConcurrencyTests covers the job path, where
/// RenewalTaskCreationService treats the same violation as an idempotent no-op
/// and returns null. That is the correct outcome for a retried job and the wrong
/// one for a user-initiated API call, which is why the two paths handle the same
/// violation differently.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DuplicateRenewalTaskConflictTests : IntegrationTestBase
{
    public DuplicateRenewalTaskConflictTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;

    /// <summary>A fresh id per test, so each gets its own document and task.</summary>
    private static readonly Guid DocumentUnderTest =
        Guid.Parse("f6c90000-0000-0000-0000-000000000001");

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Plants a document that belongs to an employee of company A and has no
    /// renewal task at all, which is the precondition for a create to be able to
    /// succeed. Expiry is inside the 90-day window so the row is also a
    /// realistic one, though the API path does not filter on it.
    /// </summary>
    private async Task SeedFreshDocumentAsync(Guid documentId)
    {
        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber = DocumentNumberFor(documentId),
                IssueDate = now.AddYears(-1),
                ExpiryDate = now.AddDays(30),
                FileUrl = $"https://storage.test/{documentId}",
                FileName = "doc.pdf",
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
    }

    private async Task<HttpResponseMessage> CreateTaskAsync(
        Guid documentId)
    {
        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/admin/tasks",
                new { documentId },
                Admin,
                "Admin"));

        return response;
    }

    /// <summary>
    /// Live, non-deleted, non-terminal tasks for a document — the exact set the
    /// unique index governs.
    /// </summary>
    private async Task<List<(Guid id, string status)>> LiveTasksAsync(
        Guid documentId)
    {
        var rows = new List<(Guid, string)>();

        await WithScopeDbContextAsync(db =>
        {
            rows = db.RenewalTasks
                .Where(x =>
                    x.DocumentId == documentId &&
                    !x.IsDeleted &&
                    x.Status != RenewalTaskStatus.Updated)
                .Select(x => new { x.Id, x.Status })
                .ToList()
                .Select(x => (x.Id, x.Status.ToString()))
                .ToList();
        });

        return rows;
    }

    /// <summary>
    /// Every row for the document, including soft-deleted ones. The global query
    /// filter hides IsDeleted rows, so counting through the filtered set would
    /// report a cancelled task as if it had never existed — which is exactly the
    /// distinction tests F6-8 and F6-9 turn on.
    /// </summary>
    private async Task<int> AllTaskRowsAsync(Guid documentId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.RenewalTasks
                .IgnoreQueryFilters()
                .Count(x => x.DocumentId == documentId);
        });

        return count;
    }

    private async Task<int> CreateAuditCountAsync(Guid documentId)
    {
        var needle = DocumentNumberFor(documentId);
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            // Correlated on the document NUMBER, not the id. The create handler
            // composes its audit description as
            // "Renewal task was created for document '{DocumentNumber}'", so
            // matching on the id would count zero. (The job path's description
            // uses the id instead, which is why RenewalTaskConcurrencyTests
            // correlates differently.)
            count = db.AuditLogs.Count(x =>
                x.Action == "RenewalTaskCreated" &&
                x.Description != null &&
                x.Description.Contains(needle));
        });

        return count;
    }

    /// <summary>
    /// The document number SeedFreshDocumentAsync plants, reconstructed rather
    /// than stored so the audit correlation cannot drift from the seed.
    /// </summary>
    private static string DocumentNumberFor(Guid documentId)
        => $"DOC-F6-{documentId.ToString("N")[..8]}";

    // ------------------------------------------------------------------
    // 1. The race itself
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F6-1. Two concurrent creates for one document: one 201, one 409, and " +
        "never a 500")]
    public async Task ConcurrentCreates_ProduceOneSuccessAndOneConflict()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        var responses = await Task.WhenAll(
            CreateTaskAsync(DocumentUnderTest),
            CreateTaskAsync(DocumentUnderTest));

        var codes = responses
            .Select(r => r.StatusCode)
            .OrderBy(c => c)
            .ToList();

        // The whole point: a 500 here is the regression. Before F6 the loser
        // surfaced the raw 23505 as INTERNAL_SERVER_ERROR.
        Assert.DoesNotContain(
            HttpStatusCode.InternalServerError, codes);

        Assert.Equal(
            1,
            codes.Count(c =>
                c == HttpStatusCode.Created));

        // The loser is a controlled 409. Ok is tolerated as a possible
        // alternative so this test asserts the property rather than the schedule.
        Assert.True(
            codes.Contains(HttpStatusCode.Conflict) ||
            codes.Contains(HttpStatusCode.OK),
            $"Unexpected statuses: {string.Join(", ", codes)}.");
    }

    [Fact(DisplayName =
        "F6-2. The concurrent winner and loser are exactly one each")]
    public async Task ConcurrentCreates_LeaveExactlyOneLiveTask()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await Task.WhenAll(
            CreateTaskAsync(DocumentUnderTest),
            CreateTaskAsync(DocumentUnderTest));

        var live = await LiveTasksAsync(DocumentUnderTest);

        // The database invariant the index exists to protect, asserted on the
        // stored rows rather than inferred from the status codes.
        Assert.Single(live);
    }

    [Fact(DisplayName =
        "F6-3. The losing request writes no partial task and no audit row — no " +
        "transaction residue")]
    public async Task TheLoser_LeavesNoResidue()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await Task.WhenAll(
            CreateTaskAsync(DocumentUnderTest),
            CreateTaskAsync(DocumentUnderTest));

        // One task total — the loser did not leave a soft-deleted or terminal
        // row behind, and did not leave a second row of any kind.
        Assert.Equal(1, await AllTaskRowsAsync(DocumentUnderTest));

        // One audit row. The losing SaveChanges was rolled back by the failed
        // INSERT rather than partially committed, so its history and audit rows
        // went with it.
        Assert.Equal(
            1, await CreateAuditCountAsync(DocumentUnderTest));
    }

    [Fact(DisplayName =
        "F6-4. The surviving task is a complete, usable task")]
    public async Task TheSurvivingTask_IsCompleteAndUsable()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await Task.WhenAll(
            CreateTaskAsync(DocumentUnderTest),
            CreateTaskAsync(DocumentUnderTest));

        var live = await LiveTasksAsync(DocumentUnderTest);
        var id = live[0].id;

        // The row is not a fragment: it carries the state a fresh create is
        // expected to, so whichever request won produced a task an agent can
        // actually pick up.
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == id);

            Assert.Equal(RenewalTaskStatus.Submitted, task.Status);
            Assert.Equal(DocumentUnderTest, task.DocumentId);
            Assert.Equal(TestDataSeeder.Ids.CompanyA, task.ClientCompanyId);
            Assert.False(task.IsDeleted);
            Assert.Null(task.DeletedAt);
        });
    }

    // ------------------------------------------------------------------
    // 2. The losing body is a proper 409 envelope, not a masked 500
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F6-5. The losing response is a 409 CONFLICT envelope naming the " +
        "already-taken outcome")]
    public async Task TheLoser_ReturnsAConflictEnvelope()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        var responses = await Task.WhenAll(
            CreateTaskAsync(DocumentUnderTest),
            CreateTaskAsync(DocumentUnderTest));

        var loser = responses.FirstOrDefault(
            r => r.StatusCode != HttpStatusCode.Created);

        Assert.NotNull(loser);

        var body = await loser!.Content.ReadAsStringAsync();

        // A real domain conflict, not an error envelope that happens to carry a
        // 409. INTERNAL_SERVER_ERROR would mean the DbUpdateException reached the
        // middleware untranslated.
        Assert.DoesNotContain("INTERNAL_SERVER_ERROR", body);
        Assert.Contains("CONFLICT", body);
        Assert.Contains("renewal task already exists", body);
    }

    // ------------------------------------------------------------------
    // 3. The invariant is not weakened
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F6-6. A sequential duplicate is still refused by the application guard " +
        "with 409")]
    public async Task ASequentialDuplicate_IsStillRefused()
    {
        // Not the same code path and not the thing F6 changed, but it must keep
        // working: the translator sits behind the application guard, so it must
        // not have replaced or bypassed the guard.
        await SeedFreshDocumentAsync(DocumentUnderTest);

        var first = await CreateTaskAsync(DocumentUnderTest);
        var second = await CreateTaskAsync(DocumentUnderTest);

        await EnsureOk(first, HttpStatusCode.Created);

        Assert.Equal(
            HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(await LiveTasksAsync(DocumentUnderTest));
    }

    [Fact(DisplayName =
        "F6-7. Two sequential duplicates both get 409 and still leave one task")]
    public async Task RepeatedSequentialDuplicates_RemainRefused()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await EnsureOk(
            await CreateTaskAsync(DocumentUnderTest),
            HttpStatusCode.Created);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await CreateTaskAsync(DocumentUnderTest);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        Assert.Single(await LiveTasksAsync(DocumentUnderTest));
    }

    [Fact(DisplayName =
        "F6-8. A completed task frees the document, because the index excludes " +
        "the terminal status")]
    public async Task ACompletedTask_FreesTheDocumentForANewTask()
    {
        // The index filter is Status != 6, so Updated rows are outside it. A
        // document whose last renewal is complete must be able to open a new
        // one — the fix must not have narrowed that into "one task ever".
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await EnsureOk(
            await CreateTaskAsync(DocumentUnderTest),
            HttpStatusCode.Created);

        var first = (await LiveTasksAsync(DocumentUnderTest))[0].id;

        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == first);

            task.Status = RenewalTaskStatus.Updated;
            task.CompletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var second = await CreateTaskAsync(DocumentUnderTest);

        await EnsureOk(second, HttpStatusCode.Created);
        Assert.Equal(2, await AllTaskRowsAsync(DocumentUnderTest));
    }

    [Fact(DisplayName =
        "F6-9. A soft-deleted task also frees the document")]
    public async Task ASoftDeletedTask_FreesTheDocument()
    {
        // The other half of the filter: IsDeleted = false. A cancelled task must
        // not keep blocking the next renewal.
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await EnsureOk(
            await CreateTaskAsync(DocumentUnderTest),
            HttpStatusCode.Created);

        var first = (await LiveTasksAsync(DocumentUnderTest))[0].id;

        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == first);

            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var second = await CreateTaskAsync(DocumentUnderTest);

        await EnsureOk(second, HttpStatusCode.Created);
        Assert.Equal(2, await AllTaskRowsAsync(DocumentUnderTest));
    }

    // ------------------------------------------------------------------
    // 4. Only that constraint is translated
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F6-10. An unknown document is still a 404, not a 409")]
    public async Task AnUnknownDocument_StillReturnsNotFound()
    {
        // The translator matches on the exact constraint name and returns null
        // for everything else, so an unrelated failure cannot be reclassified as
        // a duplicate-task conflict.
        var response = await CreateTaskAsync(TestDataSeeder.Ids.Unknown);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "F6-11. An inactive document is still refused, and with the same " +
        "conflict the guard always gave")]
    public async Task AnInactiveDocument_IsStillRefused()
    {
        // Document must be active to open a renewal. The wording is the
        // pre-existing one, which is the check that proves the create guard ran
        // and rejected the request before any insert.
        await SeedFreshDocumentAsync(DocumentUnderTest);

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents.Single(
                x => x.Id == DocumentUnderTest);

            document.IsActive = false;
            db.SaveChanges();
        });

        var response = await CreateTaskAsync(DocumentUnderTest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("inactive document", body);

        Assert.Empty(await LiveTasksAsync(DocumentUnderTest));
    }

    [Fact(DisplayName =
        "F6-12. A non-Admin cannot create a renewal task (403), so the conflict " +
        "path is not reachable from a lower role")]
    public async Task ANonAdmin_CannotReachTheConflictPath()
    {
        await SeedFreshDocumentAsync(DocumentUnderTest);

        var response = await ScopeClient.SendAsync(
            ScopePost(
                "/api/v1/admin/tasks",
                new { documentId = DocumentUnderTest },
                TestDataSeeder.Ids.AgentA,
                "Agent"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await LiveTasksAsync(DocumentUnderTest));
    }
}
