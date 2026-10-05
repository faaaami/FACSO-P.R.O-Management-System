using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// P9.1 F9 at the HTTP boundary: an over-long block reason must be a validation
/// failure (400) and not a server error (500).
///
/// The defect being closed is specific. BlockRenewalTask had no validator, and
/// it wrote the caller's reason to three columns in one SaveChanges:
/// renewal_tasks.blocked_reason (varchar 1000), renewal_task_histories.note
/// (varchar 1000) and audit_logs.description (varchar 500), the last composed
/// as "Renewal task was blocked: {reason}". Postgres raises 22001 on an
/// over-length value, that arrives as an untranslated DbUpdateException, and
/// nothing in GlobalExceptionMiddleware maps it — so a legal-looking 473
/// characters produced a 500 and rolled back the entire block.
///
/// The unit tests in BlockRenewalTaskCommandValidatorTests cover the rule
/// itself. What only an integration test can show is the end-to-end
/// consequence: the status code the caller actually receives, and that the
/// refused input leaves the task exactly as it was.
///
/// The sibling UnblockRenewalTask handler has bounded its reason at 400 for the
/// identical reason and its endpoint tests assert 400; this file is the same
/// assertion for the write side.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class BlockRenewalTaskReasonLengthApiTests : IntegrationTestBase
{
    public BlockRenewalTaskReasonLengthApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;

    /// <summary>Seeded Submitted and assigned to AgentA, so it is blockable.</summary>
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;

    private static string BlockUrl(Guid taskId)
        => $"/api/v1/agent/tasks/{taskId}/block";

    /// <summary>
    /// Sends the block with a raw JSON body so the exact reason text reaches the
    /// server unaltered, and so "field omitted" and "field present but empty"
    /// stay distinguishable — both bind to null, but they are different paths.
    /// </summary>
    private async Task<HttpResponseMessage> BlockAsync(
        Guid taskId,
        string? reason)
    {
        var body = reason is null
            ? "{}"
            : System.Text.Json.JsonSerializer.Serialize(
                new { blockedReason = reason });

        var request = ScopePatch(
            BlockUrl(taskId),
            null,
            AgentA,
            "Agent");

        request.Content =
            System.Net.Http.Json.JsonContent.Create(
                System.Text.Json.JsonDocument.Parse(body).RootElement);

        return await ScopeClient.SendAsync(request);
    }

    // ------------------------------------------------------------------
    // The defect: over-long input is refused, not fatal
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F9-1. A 401-character reason is refused with 400, not 500")]
    public async Task AnOverLongReason_IsRefusedWithBadRequest()
    {
        var response = await BlockAsync(TaskA, new string('x', 401));

        // The specific regression. Before BlockRenewalTaskCommandValidator this
        // was 500, because the over-length audit description raised PostgreSQL
        // 22001 inside SaveChanges and no middleware rule maps DbUpdateException.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "F9-2. A reason long enough to overflow description(500) on its own is " +
        "refused with 400, not 500")]
    public async Task AReasonThatOverflowsTheAuditColumn_IsRefusedWithBadRequest()
    {
        // 473 is where "Renewal task was blocked: " + reason stops fitting
        // description varchar(500). It fits note(1000) comfortably, which is
        // exactly why only the narrowest of the three columns decided the
        // outcome and turned a bad field into a failed request.
        var response = await BlockAsync(TaskA, new string('x', 473));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "F9-3. The refusal is a normal VALIDATION_ERROR envelope naming the field")]
    public async Task TheRefusal_IsAValidationErrorEnvelope()
    {
        var response = await BlockAsync(TaskA, new string('x', 401));
        var body = await response.Content.ReadAsStringAsync();

        // Proves the request was stopped by the validation gate and not by some
        // other layer that happens to produce a 400.
        Assert.Contains("VALIDATION_ERROR", body);
        Assert.Contains("BlockedReason", body);
        Assert.Contains("400", body);
    }

    // ------------------------------------------------------------------
    // The boundary is reachable
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F9-4. A reason at exactly 400 characters is accepted and reaches all " +
        "three columns")]
    public async Task AReasonAtTheLimit_IsAcceptedAndPersisted()
    {
        var boundaryReason = new string('x', 400);

        var response = await BlockAsync(TaskA, boundaryReason);

        await EnsureOk(response, HttpStatusCode.OK);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            Assert.Equal(RenewalTaskStatus.Blocked, task.Status);
            Assert.Equal(boundaryReason, task.BlockedReason);
            Assert.NotNull(task.BlockedSince);

            var history = await db.RenewalTaskHistories
                .Where(x =>
                    x.RenewalTaskId == TaskA &&
                    x.Status == RenewalTaskStatus.Blocked)
                .FirstAsync();

            Assert.Equal(boundaryReason, history.Note);
            Assert.True(history.Note!.Length <= 1000);

            var audit = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskBlocked")
                .FirstAsync();

            // "Renewal task was blocked: " is 28 characters, so 28 + 400 = 428,
            // inside description's 500. This is the assertion that could not have
            // been made before the bound existed.
            Assert.True(audit.Description!.Length <= 500);
            Assert.Contains(boundaryReason, audit.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "F9-5. A reason at the limit is still stored trimmed, so a padded " +
        "boundary value is not refused")]
    public async Task APaddedReasonAtTheLimit_IsAccepted()
    {
        var padded = "  " + new string('x', 400) + "  ";

        var response = await BlockAsync(TaskA, padded);

        await EnsureOk(response, HttpStatusCode.OK);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            // The handler trims once before storing, and the validator measures
            // the trimmed value, so the two agree. A raw-length measurement would
            // have refused this and disagreed with what is actually written.
            Assert.Equal(new string('x', 400), task.BlockedReason);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // The refusal must not have written anything
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F9-6. A refused over-long reason leaves the task and both write trails " +
        "untouched")]
    public async Task ARefusedOverLongReason_WritesNothing()
    {
        var db = await CreateScopeDbContextAsync();
        var historiesBefore = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskA);
        var auditsBefore = await db.AuditLogs
            .CountAsync(x => x.EntityId == TaskA);
        await db.DisposeAsync();

        await BlockAsync(TaskA, new string('x', 401));

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var task = await db2.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            // Validation runs in the pipeline before the handler reads the task,
            // so there is no partial block to clean up: no status change, no
            // BlockedReason, and no BlockedSince.
            Assert.Equal(RenewalTaskStatus.Submitted, task.Status);
            Assert.Null(task.BlockedReason);
            Assert.Null(task.BlockedSince);

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
        "F9-7. After a refusal the same task can still be blocked with a valid " +
        "reason")]
    public async Task TheTask_CanStillBeBlockedAfterARefusal()
    {
        await BlockAsync(TaskA, new string('x', 401));

        var response = await BlockAsync(TaskA, "A real, reasonable reason.");

        // Proves the refusal left the task genuinely usable rather than in some
        // half-blocked state.
        await EnsureOk(response, HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    // Required is preserved
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "F9-8. A blank reason is still refused with 400")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task ABlankReason_IsRefusedWithBadRequest(string reason)
    {
        var response = await BlockAsync(TaskA, reason);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "F9-9. An omitted reason is still refused with 400")]
    public async Task AnOmittedReason_IsRefusedWithBadRequest()
    {
        // An absent JSON property binds to null, a different path from an empty
        // string even though both are refused.
        var response = await BlockAsync(TaskA, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
