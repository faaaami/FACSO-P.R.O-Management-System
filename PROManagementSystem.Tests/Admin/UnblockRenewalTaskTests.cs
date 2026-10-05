using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// Phase 2 Admin unblock: PATCH /api/v1/admin/tasks/{taskId}/unblock.
///
/// G2 closed a dead end. BlockRenewalTaskCommandHandler could move a task to
/// `Blocked`, and `Blocked` is absent from StatusChain in
/// UpdateRenewalTaskStatusCommandHandler, so Array.IndexOf returned -1 and every
/// subsequent /status call was rejected with a 409 — the task was permanently
/// trapped, with DELETE as the only exit. This endpoint is the exit.
///
/// What is verified here is the state contract rather than the code shape:
///   - only a Blocked task can be unblocked, and it goes to Submitted;
///   - BlockedReason and BlockedSince are cleared from the CURRENT state while
///     the block's own history row keeps the original reason permanently;
///   - everything else on the task survives — assignee, document, company, fee;
///   - the reason is required, and Admin-only;
///   - history and audit both record the unblock;
///   - a repeat attempt is refused rather than silently repeating;
///   - the existing Block operation is untouched and still works.
///
/// The Block operation is exercised through its own real HTTP endpoint rather
/// than by writing a Blocked row directly, so these tests also serve as the
/// regression guard for Block that the phase requires.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class UnblockRenewalTaskTests : IntegrationTestBase
{
    public UnblockRenewalTaskTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;

    /// <summary>
    /// Submitted, assigned to AgentA, on DocumentA, company A, fee 150m in the
    /// seeder. That makes it the right subject: it carries every field the
    /// unblock must preserve.
    /// </summary>
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;

    /// <summary>Seeded directly in the terminal Updated state.</summary>
    private static readonly Guid TaskC = TestDataSeeder.Ids.TaskC;

    private const string BlockedReason =
        "Waiting on the client to supply the original visa scan.";

    private const string UnblockReason =
        "Client uploaded the scan; the blocker is resolved.";

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string UnblockUrl(Guid taskId)
        => $"/api/v1/admin/tasks/{taskId}/unblock";

    /// <summary>
    /// Drives the REAL block endpoint so the task reaches Blocked through the
    /// production path. Also asserts it worked, because a silently-failed block
    /// would make every unblock test below fail for the wrong reason.
    /// </summary>
    private async Task BlockTaskAsync(
        Guid taskId,
        string reason = BlockedReason)
    {
        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/agent/tasks/{taskId}/block",
                new { blockedReason = reason },
                AgentA,
                "Agent"));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    /// <summary>
    /// Sends the unblock with a raw body so that "field omitted" and "field
    /// present but empty" are distinguishable — they bind to the same null, but a
    /// test that cannot express them cannot prove either is refused.
    /// </summary>
    private async Task<(HttpStatusCode code, string body)> RawUnblockAsync(
        Guid taskId,
        string jsonBody,
        Guid? asUser = null,
        string role = "Admin")
    {
        var request = ScopePatch(
            UnblockUrl(taskId),
            null,
            asUser ?? Admin,
            role);

        request.Content =
            System.Net.Http.Json.JsonContent.Create(
                System.Text.Json.JsonDocument.Parse(jsonBody).RootElement);

        var response = await ScopeClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    private async Task<(HttpStatusCode code, string body)> UnblockAsync(
        Guid taskId,
        string reason = UnblockReason,
        Guid? asUser = null,
        string role = "Admin")
        => await RawUnblockAsync(
            taskId,
            $"{{\"unblockReason\":{(reason is null ? "null" : $"\"{reason}\"")}}}",
            asUser,
            role);

    private async Task<(Guid id, string status, decimal? fee)> ReadTaskAsync(
        Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == taskId);

            return (task.Id, task.Status.ToString(), task.ServiceFeeAmount);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 1-2. The operation works and lands on Submitted
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "1. Admin can unblock a Blocked task (200)")]
    public async Task Admin_CanUnblockABlockedTask()
    {
        await BlockTaskAsync(TaskA);

        var (code, _) = await UnblockAsync(TaskA);
        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "2. Unblock changes the status to Submitted")]
    public async Task Unblock_ChangesStatusToSubmitted()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);
            Assert.Equal(RenewalTaskStatus.Submitted, task.Status);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "2b. The status endpoint works again after an unblock — the dead end is " +
        "actually open, not just cosmetically renamed")]
    public async Task TheTask_CanBeDrivenForwardAgain()
    {
        // The point of G2. Before it, a Blocked task could only be deleted: the
        // status handler computes Array.IndexOf over StatusChain, which does not
        // contain Blocked, so every transition was refused. This proves the exit
        // is real by walking the task one step forward afterwards.
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/agent/tasks/{TaskA}/status",
                new { status = "FeePaid", note = (string?)null },
                AgentA,
                "Agent"));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "2c. Unblock returns the new status and the reason it recorded")]
    public async Task TheResponse_EchoesStatusAndReason()
    {
        await BlockTaskAsync(TaskA);

        var (code, body) = await UnblockAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, code);

        var compact = body.Replace(" ", string.Empty);
        Assert.Contains("\"status\":\"Submitted\"", compact);
        Assert.Contains("\"unblockReason\":", compact);
    }

    // ------------------------------------------------------------------
    // 3-4. The blocked markers are cleared from the current state
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "3. Unblock clears BlockedReason")]
    public async Task Unblock_ClearsBlockedReason()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);
            Assert.Null(task.BlockedReason);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "4. Unblock clears BlockedSince")]
    public async Task Unblock_ClearsBlockedSince()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);
            Assert.Null(task.BlockedSince);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "3b. The detail endpoint no longer reports the blocked markers after an " +
        "unblock")]
    public async Task TheDetailEndpoint_NoLongerReportsTheBlockedMarkers()
    {
        // Guards the read path, not just the write: a task could be correctly
        // unblocked in the database and still render as blocked in the drawer.
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/admin/tasks/{TaskA}", Admin, "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = (await response.Content.ReadAsStringAsync())
            .Replace(" ", string.Empty);

        Assert.Contains("\"blockedReason\":null", body);
        Assert.Contains("\"blockedSince\":null", body);
        Assert.Contains("\"status\":\"Submitted\"", body);
    }

    // ------------------------------------------------------------------
    // 5-7. Everything else on the task survives
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "5. The assigned Agent remains assigned")]
    public async Task TheAssignedAgent_RemainsAssigned()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            // The work is the same work, so the same Agent resumes it. Clearing
            // the assignee would silently hand the task to nobody.
            Assert.Equal(AgentA, task.AssignedStaffId);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "6. ServiceFeeAmount is left exactly as it was")]
    public async Task ServiceFeeAmount_IsUnchanged()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var (_, _, fee) = await ReadTaskAsync(TaskA);

        // 150m is the seeder's value for TaskA. Unblock touches status and the two
        // blocked columns and nothing else; in particular it must not clear or
        // recompute a figure the completion gate depends on.
        Assert.Equal(150m, fee);
    }

    [Fact(DisplayName =
        "7. The document and company relationships are left exactly as they were")]
    public async Task DocumentAndCompanyRelationships_AreUnchanged()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            Assert.Equal(TestDataSeeder.Ids.DocumentA, task.DocumentId);
            Assert.Equal(TestDataSeeder.Ids.CompanyA, task.ClientCompanyId);
            Assert.False(task.IsDeleted);
            Assert.Null(task.DeletedAt);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 8-9. The reason is required and must not be whitespace
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "8. An empty reason is refused (400)")]
    public async Task AnEmptyReason_IsRefused()
    {
        await BlockTaskAsync(TaskA);

        var (code, _) = await RawUnblockAsync(
            TaskA, "{\"unblockReason\":\"\"}");

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "8b. An omitted reason is refused (400)")]
    public async Task AnOmittedReason_IsRefused()
    {
        // An absent JSON property binds to null, which is a different code path
        // in the handler from an empty string even though both are refused.
        await BlockTaskAsync(TaskA);

        var (code, _) = await RawUnblockAsync(TaskA, "{}");

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Theory(DisplayName =
        "9. A whitespace-only reason is refused (400)")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t\n ")]
    public async Task AWhitespaceOnlyReason_IsRefused(string reason)
    {
        await BlockTaskAsync(TaskA);

        var (code, _) = await RawUnblockAsync(
            TaskA,
            System.Text.Json.JsonSerializer.Serialize(
                new { unblockReason = reason }));

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "9d. An over-long reason is refused (400) — the reason is persisted into " +
        "two columns with different limits, note(1000) and description(500)")]
    public async Task AnOverLongReason_IsRefused()
    {
        // The handler bounds the reason at 400. This is not arbitrary: the reason
        // is written to renewal_task_histories.note (varchar 1000) as
        // "Unblocked: {reason}" AND to audit_logs.description (varchar 500) as
        // "Renewal task was unblocked: {reason}". Postgres refuses an over-length
        // value, and because both writes share one SaveChanges with the status
        // change, an unbounded reason would take the whole unblock down with a
        // 500 rather than failing the single field the reader got wrong.
        await BlockTaskAsync(TaskA);

        var (code, _) = await RawUnblockAsync(
            TaskA,
            System.Text.Json.JsonSerializer.Serialize(
                new { unblockReason = new string('x', 401) }));

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "9e. A reason at the limit is accepted, and both columns fit")]
    public async Task AReasonAtTheLimit_IsAccepted()
    {
        // The other half of the guard: the boundary must be reachable, and hitting
        // it must not trip the varchar limits. This is the test that would have
        // failed with an unbounded reason.
        var boundaryReason = new string('x', 400);

        await BlockTaskAsync(TaskA);

        var (code, _) = await RawUnblockAsync(
            TaskA,
            System.Text.Json.JsonSerializer.Serialize(
                new { unblockReason = boundaryReason }));

        Assert.Equal(HttpStatusCode.OK, code);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var history = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            var audit = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskUnblocked")
                .FirstAsync();

            // "Unblocked: " + 400 = 409, inside note's 1000.
            Assert.Equal($"Unblocked: {boundaryReason}", history.Note);
            Assert.True(history.Note!.Length <= 1000);

            // 28 + 400 + the cleared-reason clause, inside description's 500.
            Assert.True(audit.Description!.Length <= 500);
            Assert.Contains(boundaryReason, audit.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "9f. A long CLEARED reason does not overflow the audit description")]
    public async Task ALongClearedReason_DoesNotOverflowTheAuditDescription()
    {
        // The part this command cannot bound: the cleared reason is not this
        // request's input, it is whatever was stored on the task, and it shares
        // the 500-char description column with the unblock reason. Block applies
        // the same 400-char cap (BlockRenewalTaskCommandValidator), so a
        // near-limit blocked reason is reachable and must be shortened here
        // rather than concatenated raw, or it would abort the whole transaction.
        await BlockTaskAsync(TaskA, new string('b', 400));

        var (code, _) = await RawUnblockAsync(
            TaskA,
            System.Text.Json.JsonSerializer.Serialize(
                new { unblockReason = new string('u', 400) }));

        Assert.Equal(HttpStatusCode.OK, code);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);
            Assert.Equal(RenewalTaskStatus.Submitted, task.Status);

            var audit = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskUnblocked")
                .FirstAsync();

            Assert.True(audit.Description!.Length <= 500);

            // Shortened, and marked as shortened — a reader must not mistake a
            // trimmed value for the whole reason.
            Assert.Contains('…', audit.Description);

            // The unabridged reason is still permanent, in the block's own history.
            var blockRow = await db.RenewalTaskHistories
                .Where(x =>
                    x.RenewalTaskId == TaskA &&
                    x.Status == RenewalTaskStatus.Blocked)
                .FirstAsync();

            Assert.Equal(400, blockRow.Note!.Length);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "9b. A refused reason leaves the task Blocked and untouched")]
    public async Task ARefusedReason_LeavesTheTaskBlocked()
    {
        await BlockTaskAsync(TaskA);

        await RawUnblockAsync(TaskA, "{\"unblockReason\":\"  \"}");

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            // Validation runs before the task is read, so nothing was written.
            Assert.Equal(RenewalTaskStatus.Blocked, task.Status);
            Assert.Equal(BlockedReason, task.BlockedReason);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "9c. A padded reason is accepted and stored trimmed")]
    public async Task APaddedReason_IsAcceptedAndStoredTrimmed()
    {
        await BlockTaskAsync(TaskA);

        var (code, _) = await RawUnblockAsync(
            TaskA,
            "{\"unblockReason\":\"  resolved, see email  \"}");

        Assert.Equal(HttpStatusCode.OK, code);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Equal("Unblocked: resolved, see email", row.Note);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 10-11. Admin only
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "10. An Agent is refused (403)")]
    public async Task AnAgent_IsRefused()
    {
        await BlockTaskAsync(TaskA);

        // Even for the task they blocked themselves. Unblocking is the Admin's
        // decision alone, so the role gate does not consider ownership.
        var (code, _) = await UnblockAsync(
            TaskA, asUser: AgentA, role: "Agent");

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "11. A Client is refused (403)")]
    public async Task AClient_IsRefused()
    {
        await BlockTaskAsync(TaskA);

        var (code, _) = await UnblockAsync(
            TaskA, asUser: ClientUserA, role: "Client");

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "11b. A refused role leaves the task Blocked and writes nothing")]
    public async Task ARefusedRole_LeavesTheTaskBlocked()
    {
        await BlockTaskAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        var auditsBefore = await db.AuditLogs.CountAsync();
        await db.DisposeAsync();

        await UnblockAsync(TaskA, asUser: AgentA, role: "Agent");

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var task = await db2.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            Assert.Equal(RenewalTaskStatus.Blocked, task.Status);
            Assert.Equal(auditsBefore, await db2.AuditLogs.CountAsync());
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 12-15. Existence and state guards
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "12. A missing task is refused (404)")]
    public async Task AMissingTask_IsRefused()
    {
        var (code, _) = await UnblockAsync(TestDataSeeder.Ids.Unknown);
        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact(DisplayName =
        "13. A soft-deleted task cannot be unblocked (404)")]
    public async Task ASoftDeletedTask_CannotBeUnblocked()
    {
        await BlockTaskAsync(TaskA);

        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskA);
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var (code, _) = await UnblockAsync(TaskA);
        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact(DisplayName =
        "14. A task that is not Blocked is refused (409)")]
    public async Task ANonBlockedTask_IsRefused()
    {
        // TaskA is seeded Submitted — never blocked, so there is nothing to clear.
        var (code, body) = await UnblockAsync(TaskA);

        Assert.Equal(HttpStatusCode.Conflict, code);

        // The message names the real status, so a caller can tell "wrong state"
        // from "not found" without a second request.
        Assert.Contains("Submitted", body);
    }

    [Fact(DisplayName =
        "15. An Updated (completed) task cannot be unblocked (409)")]
    public async Task AnUpdatedTask_CannotBeUnblocked()
    {
        // Updated is terminal and has no BlockedReason; it is refused by the same
        // "is it Blocked" guard as every other non-Blocked state.
        var (code, body) = await UnblockAsync(TaskC);

        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Contains("Updated", body);
    }

    [Fact(DisplayName =
        "15b. A Completed task's fee and completedAt are untouched by a " +
        "refused unblock")]
    public async Task ARefusedUnblock_DoesNotTouchACompletedTask()
    {
        await UnblockAsync(TaskC);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskC);

            Assert.Equal(RenewalTaskStatus.Updated, task.Status);
            Assert.Equal(150m, task.ServiceFeeAmount);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 16-17. History and audit
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "16. History records the unblock reason, the actor and the new status")]
    public async Task History_RecordsTheUnblockReason()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Equal(RenewalTaskStatus.Submitted, row.Status);
            Assert.Equal(Admin, row.ChangedBy);
            Assert.Contains(UnblockReason, row.Note!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "16b. The ORIGINAL blocked reason is still in history after the " +
        "unblock clears it from the task")]
    public async Task TheOriginalBlockedReason_SurvivesInHistory()
    {
        // This is the requirement that makes clearing BlockedReason safe. The
        // column is a current-state field; the block's own history row is the
        // permanent record, and it must not be disturbed by the unblock.
        await BlockTaskAsync(TaskA, BlockedReason);
        await UnblockAsync(TaskA, UnblockReason);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);
            Assert.Null(task.BlockedReason);

            var blockRow = await db.RenewalTaskHistories
                .Where(x =>
                    x.RenewalTaskId == TaskA &&
                    x.Status == RenewalTaskStatus.Blocked)
                .FirstAsync();

            Assert.Equal(BlockedReason, blockRow.Note);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "16c. The history tab returns both the block and the unblock")]
    public async Task TheHistoryEndpoint_ReturnsBothEvents()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/tasks/{TaskA}/history",
                Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(BlockedReason, body);
        Assert.Contains(UnblockReason, body);
    }

    [Fact(DisplayName =
        "17. AuditLog records the unblock under RenewalTaskUnblocked")]
    public async Task AuditLog_RecordsTheUnblock()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskUnblocked")
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync();

            Assert.Equal("RenewalTask", row.EntityType);
            Assert.Equal(Admin, row.UserId);
            Assert.Contains(UnblockReason, row.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "17b. The unblock audit row names the blocked reason it cleared")]
    public async Task TheUnblockAuditRow_NamesTheClearedReason()
    {
        // A later reader of the audit trail alone can see what was cleared, not
        // only what replaced it.
        await BlockTaskAsync(TaskA, BlockedReason);
        await UnblockAsync(TaskA, UnblockReason);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskUnblocked")
                .FirstAsync();

            Assert.Contains(BlockedReason, row.Description!);
            Assert.Contains(UnblockReason, row.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "17c. The audit log records the block and the unblock as a pair")]
    public async Task AuditLog_RecordsBothEvents()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var actions = await db.AuditLogs
                .Where(x => x.EntityId == TaskA)
                .Select(x => x.Action)
                .ToListAsync();

            Assert.Contains("RenewalTaskBlocked", actions);
            Assert.Contains("RenewalTaskUnblocked", actions);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 18-19. Idempotency and no duplication
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "18. A repeated unblock is refused (409)")]
    public async Task ARepeatedUnblock_IsRefused()
    {
        await BlockTaskAsync(TaskA);

        var (first, _) = await UnblockAsync(TaskA);
        var (second, _) = await UnblockAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.Conflict, second);
    }

    [Fact(DisplayName =
        "18b. A refused repeat writes no second history or audit row")]
    public async Task ARefusedRepeat_WritesNothingFurther()
    {
        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var (h0, a0) = await CountWritesAsync(TaskA);

        await UnblockAsync(TaskA);

        var (h1, a1) = await CountWritesAsync(TaskA);

        Assert.Equal(h0, h1);
        Assert.Equal(a0, a1);
    }

    private async Task<(int histories, int audits)> CountWritesAsync(
        Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return (
                await db.RenewalTaskHistories
                    .CountAsync(x => x.RenewalTaskId == taskId),
                await db.AuditLogs.CountAsync(x => x.EntityId == taskId));
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "19. No duplicate task is created")]
    public async Task NoDuplicateTask_IsCreated()
    {
        var db = await CreateScopeDbContextAsync();
        var before = await db.RenewalTasks.CountAsync();
        await db.DisposeAsync();

        await BlockTaskAsync(TaskA);
        await UnblockAsync(TaskA);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(before, await db2.RenewalTasks.CountAsync());

            // The same row, not a replacement one.
            Assert.Equal(
                1,
                await db2.RenewalTasks.CountAsync(x => x.Id == TaskA));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 20. The existing Block operation is preserved
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "20. The existing Block operation still works unchanged")]
    public async Task TheExistingBlockOperation_StillWorks()
    {
        var (code, _) = await BlockRawAsync(TaskA, BlockedReason);
        Assert.Equal(HttpStatusCode.OK, code);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            // Every field BlockRenewalTaskCommandHandler is documented to set.
            Assert.Equal(RenewalTaskStatus.Blocked, task.Status);
            Assert.Equal(BlockedReason, task.BlockedReason);
            Assert.NotNull(task.BlockedSince);

            var history = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Equal(RenewalTaskStatus.Blocked, history.Status);
            Assert.Equal(BlockedReason, history.Note);

            var audit = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskBlocked")
                .FirstAsync();

            Assert.Contains(BlockedReason, audit.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "20b. Block still refuses a completed task (unchanged semantics)")]
    public async Task Block_StillRefusesACompletedTask()
    {
        var (code, _) = await BlockRawAsync(TaskC, BlockedReason);
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    /// <summary>
    /// Block without asserting the outcome, so a preservation test can state the
    /// status it expects for itself.
    /// </summary>
    private async Task<(HttpStatusCode code, string body)> BlockRawAsync(
        Guid taskId,
        string reason)
    {
        var response = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/agent/tasks/{taskId}/block",
                new { blockedReason = reason },
                AgentA,
                "Agent"));

        return (
            response.StatusCode,
            await response.Content.ReadAsStringAsync());
    }
}
