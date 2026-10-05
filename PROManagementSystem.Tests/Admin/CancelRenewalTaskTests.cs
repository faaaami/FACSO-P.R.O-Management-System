using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// P9.1 F8: DELETE /api/v1/admin/tasks/{taskId}, the renewal-task cancellation.
///
/// What this closes, and what it deliberately does not. The audit that scoped
/// this work first suspected an IDOR, because CancelRenewalTaskCommandHandler
/// looks up the task by id with no ownership check. It is not one: TasksController
/// carries [Authorize(Roles = "Admin")] at the class level, and this is the only
/// task DELETE in the API — AgentTasksController exposes none. The missing
/// handler-side guard is defence in depth behind a role gate that already holds.
/// The role tests at the end of this file exist to keep it that way.
///
/// The three real defects were all about what a completed piece of work can lose:
///
///   1. There was no terminal guard, so a task in `Updated` — the terminal state
///      of the chain, and the value the IX_renewal_tasks_DocumentId unique index
///      excludes as Status != 6 — could be soft-deleted. That hides the
///      completion record, the renewed document date, and the Service Fee invoice
///      created at completion, behind a cancellation that reads as "this never
///      happened". Block and Unblock both already refused to move a task out of
///      Updated; cancel was the one mutation that did not.
///
///   2. Cancellation wrote an audit row but no RenewalTaskHistory row, while every
///      sibling task mutation writes one. The history tab and
///      GET /admin/tasks/{id}/history are built on that table, so a cancelled task
///      simply stopped existing on the timeline with no entry saying why.
///
///   3. The audit description was the bare string "Renewal task was cancelled.",
///      identifying nothing but the entity id, where the sibling lifecycle
///      handlers name the document.
///
/// The already-cancelled case deliberately keeps its existing
/// InvalidOperationException, which GlobalExceptionMiddleware already maps to 409.
/// This is a tightening of a completed-task cancel, not a redefinition of
/// cancellation, so nothing that returned a sensible code before changes shape.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class CancelRenewalTaskTests : IntegrationTestBase
{
    public CancelRenewalTaskTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;

    /// <summary>Seeded Submitted on DocumentA ("DOC-A-001"), company A, AgentA.</summary>
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;

    /// <summary>Seeded directly in the terminal Updated state.</summary>
    private static readonly Guid TaskC = TestDataSeeder.Ids.TaskC;

    private const string DocumentANumber = "DOC-A-001";

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string CancelUrl(Guid taskId)
        => $"/api/v1/admin/tasks/{taskId}";

    /// <summary>
    /// IntegrationTestBase has no ScopeDelete, so the request is built directly
    /// with the same identity header the shared helpers apply.
    /// </summary>
    private async Task<(HttpStatusCode code, string body)> CancelAsync(
        Guid taskId,
        Guid? asUser = null,
        string role = "Admin")
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            CancelUrl(taskId));

        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                asUser ?? Admin, role, null));

        var response = await ScopeClient.SendAsync(request);

        return (
            response.StatusCode,
            await response.Content.ReadAsStringAsync());
    }

    private async Task<(bool isDeleted, DateTime? deletedAt, string status)>
        ReadTaskAsync(Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            // IgnoreQueryFilters: a cancelled task is exactly the row the default
            // filter hides, and asserting on it requires seeing past the filter.
            var task = await db.RenewalTasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == taskId);

            return (task.IsDeleted, task.DeletedAt, task.Status.ToString());
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 1. The happy path still works
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "1. Admin can cancel an active Submitted task (200)")]
    public async Task Admin_CanCancelAnActiveTask()
    {
        var (code, _) = await CancelAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "2. Cancellation is a soft delete, not a physical removal")]
    public async Task Cancellation_IsASoftDelete()
    {
        await CancelAsync(TaskA);

        var (isDeleted, deletedAt, _) = await ReadTaskAsync(TaskA);

        // The row survives. Every read path in the app filters IsDeleted, so this
        // is what makes the task disappear from the UI while the data — and the
        // renewal it recorded — is still recoverable.
        Assert.True(isDeleted);
        Assert.NotNull(deletedAt);
    }

    [Fact(DisplayName =
        "2b. The status itself is left alone by a cancellation")]
    public async Task TheStatus_IsUnchanged()
    {
        await CancelAsync(TaskA);

        var (_, _, status) = await ReadTaskAsync(TaskA);

        // Cancellation is a soft delete, not a state. There is no Cancelled
        // member in RenewalTaskStatus and none was invented: the task was
        // Submitted when it was cancelled and stays Submitted, with IsDeleted
        // carrying the cancellation.
        Assert.Equal(nameof(RenewalTaskStatus.Submitted), status);
    }

    [Fact(DisplayName =
        "2c. The response reports the cancellation")]
    public async Task TheResponse_ReportsTheCancellation()
    {
        var (code, body) = await CancelAsync(TaskA);
        var compact = body.Replace(" ", string.Empty);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("\"isDeleted\":true", compact);
        Assert.Contains("\"deletedAt\":", compact);
    }

    // ------------------------------------------------------------------
    // 3. The terminal guard
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "3. A completed Updated task cannot be cancelled (409)")]
    public async Task ACompletedTask_CannotBeCancelled()
    {
        // The core of F8. TaskC is seeded Updated, which is terminal and is the
        // value the unique index excludes.
        var (code, body) = await CancelAsync(TaskC);

        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Contains("completed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "3b. A refused completed-task cancel leaves the task intact — status, " +
        "fee, completedAt and the document renewal all survive")]
    public async Task ARefusedCancel_LeavesACompletedTaskIntact()
    {
        var db = await CreateScopeDbContextAsync();
        var feeBefore = await db.RenewalTasks
            .Where(x => x.Id == TaskC)
            .Select(x => x.ServiceFeeAmount)
            .FirstAsync();
        var completedAtBefore = await db.RenewalTasks
            .Where(x => x.Id == TaskC)
            .Select(x => x.CompletedAt)
            .FirstAsync();
        await db.DisposeAsync();

        await CancelAsync(TaskC);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var task = await db2.RenewalTasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == TaskC);

            Assert.False(task.IsDeleted);
            Assert.Null(task.DeletedAt);
            Assert.Equal(RenewalTaskStatus.Updated, task.Status);
            Assert.Equal(feeBefore, task.ServiceFeeAmount);
            Assert.Equal(completedAtBefore, task.CompletedAt);
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "3c. A refused completed-task cancel writes no history and no audit")]
    public async Task ARefusedCancel_WritesNothing()
    {
        var db = await CreateScopeDbContextAsync();
        var historiesBefore = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskC);
        var auditsBefore = await db.AuditLogs
            .CountAsync(x => x.EntityId == TaskC);
        await db.DisposeAsync();

        await CancelAsync(TaskC);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                historiesBefore,
                await db2.RenewalTaskHistories
                    .CountAsync(x => x.RenewalTaskId == TaskC));

            Assert.Equal(
                auditsBefore,
                await db2.AuditLogs.CountAsync(x => x.EntityId == TaskC));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Theory(DisplayName =
        "3d. Every non-terminal state can still be cancelled — the guard refuses " +
        "Updated only, and does not over-reach")]
    [InlineData(RenewalTaskStatus.Submitted)]
    [InlineData(RenewalTaskStatus.FeePaid)]
    [InlineData(RenewalTaskStatus.AwaitingApproval)]
    [InlineData(RenewalTaskStatus.Blocked)]
    [InlineData(RenewalTaskStatus.Approved)]
    public async Task ANonTerminalTask_CanBeCancelled(
        RenewalTaskStatus status)
    {
        // The other half of the guard: a terminal-state check written as a
        // permissive list would silently start refusing work that should be
        // cancellable. Every state on the chain before Updated must stay
        // cancellable, or this fix would have broken a legitimate operation.
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskA);
            task.Status = status;
            db.SaveChanges();
        });

        var (code, _) = await CancelAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, code);
    }

    // ------------------------------------------------------------------
    // 4. Already cancelled, and missing
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "4. Cancelling an already-cancelled task is refused (404)")]
    public async Task AnAlreadyCancelledTask_IsRefused()
    {
        // 404, not 409, and that is the pre-existing behaviour rather than
        // something this change introduced. CancelRenewalTaskCommandHandler looks
        // the task up by id alone, so the global IsDeleted query filter has
        // already removed the cancelled row before the handler's own IsDeleted
        // check is reached; the handler then throws KeyNotFoundException, which
        // maps to 404.
        //
        // This is the same shape UnblockRenewalTask documents for a soft-deleted
        // task: a deleted task is indistinguishable from a missing one, and a
        // 404 is the correct answer because it does not confirm that a cancelled
        // task ever existed. It also means the handler's IsDeleted branch is
        // currently unreachable — worth reporting, but out of scope here, since
        // P9.1 only tightens what may be cancelled and not how.
        var (first, _) = await CancelAsync(TaskA);
        var (second, _) = await CancelAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.NotFound, second);
    }

    [Fact(DisplayName =
        "4b. A refused repeat writes no second history or audit row")]
    public async Task ARefusedRepeat_WritesNothing()
    {
        await CancelAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        var historiesBefore = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskA);
        var auditsBefore = await db.AuditLogs
            .CountAsync(x => x.EntityId == TaskA);
        await db.DisposeAsync();

        await CancelAsync(TaskA);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                historiesBefore,
                await db2.RenewalTaskHistories
                    .CountAsync(x => x.RenewalTaskId == TaskA));

            Assert.Equal(
                auditsBefore,
                await db2.AuditLogs.CountAsync(x => x.EntityId == TaskA));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "5. A missing task is refused (404)")]
    public async Task AMissingTask_IsRefused()
    {
        var (code, _) = await CancelAsync(TestDataSeeder.Ids.Unknown);

        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    // ------------------------------------------------------------------
    // 6. History — the gap F8 closes
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "6. A successful cancellation writes a history row")]
    public async Task ASuccessfulCancel_WritesAHistoryRow()
    {
        var db = await CreateScopeDbContextAsync();
        var before = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskA);
        await db.DisposeAsync();

        await CancelAsync(TaskA);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                before + 1,
                await db2.RenewalTaskHistories
                    .CountAsync(x => x.RenewalTaskId == TaskA));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "6b. The history row records the actor, the time and what happened")]
    public async Task TheHistoryRow_RecordsTheCancellation()
    {
        await CancelAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Equal(Admin, row.ChangedBy);
            Assert.NotEqual(default, row.ChangedAt);

            // The status at the moment of cancellation. There is no Cancelled
            // member in the enum, so the row is distinguished by its note.
            Assert.Equal(RenewalTaskStatus.Submitted, row.Status);
            Assert.Equal("Cancelled", row.Note);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "6c. The history row records the status the task actually held, for a " +
        "non-Submitted task")]
    public async Task TheHistoryRow_RecordsTheActualStatus()
    {
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskA);
            task.Status = RenewalTaskStatus.AwaitingApproval;
            db.SaveChanges();
        });

        await CancelAsync(TaskA);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var row = await db2.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            // Not hard-coded to Submitted: the row is a faithful record of the
            // state at cancellation, whatever that state was.
            Assert.Equal(
                RenewalTaskStatus.AwaitingApproval, row.Status);
            Assert.Equal("Cancelled", row.Note);
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "7. The history endpoint reports a cancelled task as gone (404) — the " +
        "history row persists in the database")]
    public async Task TheHistoryEndpoint_ReportsACancelledTaskAsGone()
    {
        // The task is soft-deleted, and every read path in this system filters
        // IsDeleted. GetHistoryAsync in particular counts renewal_tasks with
        // is_deleted = false before returning any rows, so a cancelled task
        // yields zero and the endpoint answers 404 — deliberately, for the same
        // reason the write side does not confirm a deleted task exists.
        //
        // So the history row written by F8 is not user-visible through this
        // endpoint after a cancel, and asserting otherwise would be asserting a
        // behaviour change that was not asked for. What the fix guarantees is
        // that the record exists: the pre-cancel audit trail is no longer left
        // with a silent unexplained gap in the database, and any future
        // "show cancelled tasks" surface has the data it needs. Tests 6, 6b and
        // 6c assert exactly that, and this test pins the current read
        // behaviour so the distinction stays deliberate.
        await CancelAsync(TaskA);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/tasks/{TaskA}/history",
                Admin,
                "Admin"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var db = await CreateScopeDbContextAsync();
        try
        {
            // The row is there, and it is not itself deleted.
            var row = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Equal("Cancelled", row.Note);
            Assert.False(row.IsDeleted);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 8. Audit — the other gap F8 closes
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "8. A successful cancellation writes an audit row naming the document")]
    public async Task TheAuditRow_NamesTheDocument()
    {
        await CancelAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskCancelled")
                .FirstAsync();

            Assert.Equal("RenewalTask", row.EntityType);
            Assert.Equal(Admin, row.UserId);

            // The improvement. "Renewal task was cancelled." named nothing a
            // reader could recognise; the document number is what identifies the
            // renewal in the audit trail, exactly as the create, block and
            // unblock descriptions do.
            Assert.Contains(DocumentANumber, row.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "8b. The audit action is unchanged, so existing audit readers keep " +
        "working")]
    public async Task TheAuditAction_IsUnchanged()
    {
        await CancelAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x => x.EntityId == TaskA)
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync();

            // Deliberately the pre-existing action string. Only the description
            // gained the document number; renaming the action would silently
            // break every consumer that filters on it.
            Assert.Equal("RenewalTaskCancelled", row.Action);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "8c. The audit description stays inside the varchar(500) column")]
    public async Task TheAuditDescription_FitsTheColumn()
    {
        await CancelAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskCancelled")
                .FirstAsync();

            // document_number is varchar(100), so the composed description has a
            // fixed ceiling of about 150 characters whatever the document is.
            Assert.True(row.Description!.Length <= 500);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "8d. A cancellation of a task whose document is soft-deleted still " +
        "succeeds and still names the document")]
    public async Task ADocumentSoftDeleted_StillYieldsAnAuditDescription()
    {
        // The document lookup ignores query filters precisely so this works: a
        // cancelled renewal whose document has since been deleted still produces
        // a usable audit row rather than a bare sentence, and never a 500.
        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents.Single(
                x => x.Id == TestDataSeeder.Ids.DocumentA);

            document.IsDeleted = true;
            document.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var (code, _) = await CancelAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, code);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var row = await db2.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskCancelled")
                .FirstAsync();

            Assert.Contains(DocumentANumber, row.Description!);
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 9. Admin only — the boundary this work must not disturb
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "9. An Agent is refused (403), including for their own task")]
    public async Task AnAgent_IsRefused()
    {
        // TaskA is assigned to AgentA, so ownership is not the question: the
        // role gate does not consider it. Cancellation is an Admin decision.
        var (code, _) = await CancelAsync(
            TaskA, asUser: AgentA, role: "Agent");

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "10. A Client is refused (403)")]
    public async Task AClient_IsRefused()
    {
        var (code, _) = await CancelAsync(
            TaskA, asUser: ClientUserA, role: "Client");

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "10b. A refused role cancels nothing and writes nothing")]
    public async Task ARefusedRole_WritesNothing()
    {
        var db = await CreateScopeDbContextAsync();
        var historiesBefore = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskA);
        var auditsBefore = await db.AuditLogs
            .CountAsync(x => x.EntityId == TaskA);
        await db.DisposeAsync();

        await CancelAsync(TaskA, asUser: AgentA, role: "Agent");
        await CancelAsync(TaskA, asUser: ClientUserA, role: "Client");

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var task = await db2.RenewalTasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == TaskA);

            Assert.False(task.IsDeleted);
            Assert.Equal(
                historiesBefore,
                await db2.RenewalTaskHistories
                    .CountAsync(x => x.RenewalTaskId == TaskA));

            Assert.Equal(
                auditsBefore,
                await db2.AuditLogs.CountAsync(x => x.EntityId == TaskA));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "11. No Agent route can cancel a task — cancellation is not reachable " +
        "without the Admin role")]
    public async Task Cancellation_IsNotReachableThroughAnyAgentRoute()
    {
        // The audit that scoped this work suspected an IDOR, because the handler
        // has no ownership guard. The guard that actually holds is the
        // class-level [Authorize(Roles = "Admin")] on TasksController, so the
        // defence is the absence of any Agent-side cancellation surface. This
        // pins that: the documented Agent task base URL offers no delete.
        foreach (var url in new[]
                 {
                     $"/api/v1/agent/tasks/{TaskA}",
                     $"/api/v1/agent/tasks/{TaskA}/cancel"
                 })
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete, url);

            request.Headers.Add(
                "X-Test-Identity",
                TestIdentityService.HeaderPayload(AgentA, "Agent", null));

            var response = await ScopeClient.SendAsync(request);

            // 403, 404 or 405, but never a success. 405 means the path matched a
            // route that has no DELETE; 404 means no route matched at all, which
            // is what ASP.NET returns for DELETE on /agent/tasks/{id} because
            // AgentTasksController defines no delete action; 403 would mean an
            // Agent route existed and was role-gated. Were a cancel route ever
            // added on the Agent side, this would become a 200 or 204 and fail,
            // which is the point.
            Assert.True(
                response.StatusCode is HttpStatusCode.MethodNotAllowed
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.NotFound,
                $"{url} returned {response.StatusCode}.");
        }

        // And the task is untouched throughout.
        var (isDeleted, _, _) = await ReadTaskAsync(TaskA);
        Assert.False(isDeleted);
    }
}
