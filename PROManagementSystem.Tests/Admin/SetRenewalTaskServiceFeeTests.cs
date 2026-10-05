using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// Phase 1 service-fee write path: PATCH /api/v1/admin/tasks/{taskId}/service-fee.
///
/// This endpoint exists because the status machine could not be completed.
/// UpdateRenewalTaskStatusCommandHandler requires `ServiceFeeAmount > 0` before a
/// task may move to `Updated`, and before this endpoint nothing reachable through
/// the API wrote that field — only dev seeders and test fixtures did. So the last
/// status was unreachable in any API-driven environment. The gate itself is
/// unchanged and still tested by StatusMachineTests; what is new is a way to
/// satisfy it.
///
/// Covered here: the happy path and its persistence, the Set-vs-Change
/// distinction, the terminal-state freeze, the amount rules, the role gate, the
/// soft-delete 404, the history and audit rows, that the fee is reachable on a
/// blocked task, and that the detail endpoint returns the figure.
///
/// The pure validation rules are unit-tested separately in
/// Validation/SetRenewalTaskServiceFeeCommandValidatorTests.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SetRenewalTaskServiceFeeTests : IntegrationTestBase
{
    public SetRenewalTaskServiceFeeTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;

    /// <summary>Submitted, fee already 150m in the seeder.</summary>
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;

    /// <summary>Submitted, fee already 150m in the seeder.</summary>
    private static readonly Guid TaskB = TestDataSeeder.Ids.TaskB;

    /// <summary>Seeded directly in the Updated state.</summary>
    private static readonly Guid TaskC = TestDataSeeder.Ids.TaskC;

    private static string ServiceFeeUrl(Guid taskId)
        => $"/api/v1/admin/tasks/{taskId}/service-fee";

    private async Task<(HttpStatusCode code, string body)> PatchFeeAsync(
        Guid taskId,
        decimal amount,
        Guid? asUser = null,
        string role = "Admin")
    {
        var request = ScopePatch(
            ServiceFeeUrl(taskId),
            new { amount },
            asUser ?? Admin,
            role);

        var response = await ScopeClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    // ------------------------------------------------------------------
    // Happy path
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin can set the service fee on a non-completed task (200)")]
    public async Task Admin_CanSetTheFee()
    {
        var (code, _) = await PatchFeeAsync(TaskA, 2750.50m);
        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "The new amount is persisted on the task")]
    public async Task TheAmount_IsPersisted()
    {
        await PatchFeeAsync(TaskA, 2750.50m);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks
                .SingleAsync(x => x.Id == TaskA);

            Assert.Equal(2750.50m, task.ServiceFeeAmount);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "The response echoes the stored amount and reports it as a change")]
    public async Task TheResponse_EchoesTheAmount_AndReportsAChange()
    {
        // The seeder gives TaskA a fee of 150m, so this is a change and not a
        // first set — which is what isChange exists to distinguish.
        var (code, body) = await PatchFeeAsync(TaskA, 2750.50m);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("\"serviceFeeAmount\":2750.50", body.Replace(" ", string.Empty));
        Assert.Contains("\"isChange\":true", body.Replace(" ", string.Empty));
    }

    [Fact(DisplayName =
        "A first set is reported as isChange=false, not as a change from nothing")]
    public async Task AFirstSet_IsNotAChange()
    {
        // The seeder sets a fee on every task, so the "never set" case has to be
        // created explicitly. Without this the distinction the UI labels with
        // Set-vs-Change would be untested.
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskA);
            task.ServiceFeeAmount = null;
            db.SaveChanges();
        });

        var (code, body) = await PatchFeeAsync(TaskA, 500m);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("\"isChange\":false", body.Replace(" ", string.Empty));
    }

    [Fact(DisplayName =
        "The amount survives a round trip through the detail endpoint")]
    public async Task TheAmount_IsReturnedByTheDetailEndpoint()
    {
        // This is the Dapper mapping, not the command: the detail DTO carries the
        // field and RenewalTaskReadRepository selects it. A break here would leave
        // the write working and every Admin screen showing nothing.
        await PatchFeeAsync(TaskA, 2750.50m);

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/tasks/{TaskA}",
                Admin,
                "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"serviceFeeAmount\":2750.50", body.Replace(" ", string.Empty));
    }

    [Fact(DisplayName =
        "The fee can be set on a Blocked task")]
    public async Task TheFee_CanBeSetOnABlockedTask()
    {
        // Blocked sits outside the status chain, so it is not a state this fee
        // advances — but refusing the amount there would re-create the very
        // deadlock this endpoint removes, since a blocked task still has to be
        // finished eventually.
        await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/agent/tasks/{TaskA}/block",
                new { blockedReason = "Waiting on client" },
                AgentA,
                "Agent"));

        var (code, _) = await PatchFeeAsync(TaskA, 900m);
        Assert.Equal(HttpStatusCode.OK, code);
    }

    // ------------------------------------------------------------------
    // Terminal state
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The fee of a completed (Updated) task is frozen (409)")]
    public async Task ACompletedTask_RefusesAFeeChange()
    {
        var (code, _) = await PatchFeeAsync(TaskC, 500m);
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "A refused fee change leaves the stored amount untouched")]
    public async Task ARefusedChange_LeavesTheAmountUntouched()
    {
        var before = 150m; // the seeder's value for TaskC

        await PatchFeeAsync(TaskC, 500m);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskC);
            Assert.Equal(before, task.ServiceFeeAmount);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // Amount rules, through the pipeline
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A zero amount is refused (400)")]
    public async Task ZeroAmount_IsRefused()
    {
        // The completion gate refuses ServiceFeeAmount <= 0, so persisting a zero
        // would store a figure this module could never use.
        var (code, _) = await PatchFeeAsync(TaskA, 0m);
        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "A negative amount is refused (400)")]
    public async Task NegativeAmount_IsRefused()
    {
        var (code, _) = await PatchFeeAsync(TaskA, -100m);
        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "An amount with a third decimal place is refused (400)")]
    public async Task ThreeDecimalPlaces_AreRefused()
    {
        // renewal_tasks.ServiceFeeAmount is a plain nullable numeric, so nothing
        // at the storage layer would round this — but the invoice created at
        // completion is numeric(18,2) and would. Refusing beats rounding silently.
        var (code, _) = await PatchFeeAsync(TaskA, 1500.555m);
        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    // ------------------------------------------------------------------
    // Authorization and existence
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A non-Admin role is refused (403)")]
    public async Task NonAdmin_IsRefused()
    {
        // The route is [Authorize(Roles = "Admin")] and the handler re-checks
        // IsAdmin, because the command is a plain IRequest that ISender can
        // deliver from anywhere in the application.
        var (code, _) = await PatchFeeAsync(
            TaskA, 500m, asUser: AgentA, role: "Agent");

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "An unknown task id is refused (404)")]
    public async Task UnknownTask_IsRefused()
    {
        var (code, _) = await PatchFeeAsync(TestDataSeeder.Ids.Unknown, 500m);
        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact(DisplayName =
        "A soft-deleted task is refused (404), and is indistinguishable from " +
        "a missing one")]
    public async Task SoftDeletedTask_IsRefused()
    {
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskB);
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var (code, _) = await PatchFeeAsync(TaskB, 500m);
        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    // ------------------------------------------------------------------
    // Side effects
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Setting the fee records a history row carrying the task's " +
        "UNCHANGED status")]
    public async Task SettingTheFee_RecordsAHistoryRow()
    {
        await PatchFeeAsync(TaskA, 2750.50m);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            // Not a transition: the History tab is an event log, and this is an
            // attribute change, so the row repeats the status the task holds.
            Assert.Equal(RenewalTaskStatus.Submitted, row.Status);
            Assert.Equal(Admin, row.ChangedBy);
            Assert.Contains("Service fee", row.Note!);
            Assert.Contains("2750.50", row.Note!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "A first set and a later change are worded differently, so the " +
        "history never implies a prior value that did not exist")]
    public async Task AFirstSet_IsWordedAsASet()
    {
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskA);
            task.ServiceFeeAmount = null;
            db.SaveChanges();
        });

        await PatchFeeAsync(TaskA, 500m);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var row = await db2.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Contains("set to", row.Note!);
            Assert.DoesNotContain("changed from", row.Note!);
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "A change records the audit row with the new and previous figures")]
    public async Task SettingTheFee_WritesAnAuditRow()
    {
        await PatchFeeAsync(TaskA, 2750.50m);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x => x.EntityId == TaskA
                            && x.Action == "RenewalTaskServiceFeeSet")
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync();

            Assert.Equal("RenewalTask", row.EntityType);
            Assert.Equal(Admin, row.UserId);
            Assert.Contains("2750.50", row.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "A refused write leaves no history or audit row behind")]
    public async Task ARefusedWrite_LeavesNoTrace()
    {
        // The amount, the history row and the audit row commit in one
        // SaveChanges, so a refusal must leave all three untouched rather than
        // recording an edit that did not happen.
        var db = await CreateScopeDbContextAsync();
        try
        {
            var historiesBefore = await db.RenewalTaskHistories
                .CountAsync(x => x.RenewalTaskId == TaskC);
            var auditsBefore = await db.AuditLogs
                .CountAsync(x => x.EntityId == TaskC);

            await PatchFeeAsync(TaskC, 500m);

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
        finally
        {
            await db.DisposeAsync();
        }
    }
}
