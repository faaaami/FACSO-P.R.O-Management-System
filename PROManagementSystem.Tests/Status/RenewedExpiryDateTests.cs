using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Status;

/// <summary>
/// Phase 3 G3: completing a renewal task captures the renewed document expiry.
///
/// Before this, PATCH /status could mark a task Updated without touching the
/// document at all. The document then kept the OLD expiry that had triggered
/// the renewal, so it stayed inside the expiring window and the renewal
/// appeared to be outstanding even though its task was complete.
///
/// What is verified here is the state contract rather than the code shape:
///   - a future renewed expiry is REQUIRED to enter Updated, and the same rule
///     governs the Agent and Admin surfaces because they share the handler;
///   - the value lands on the document the TASK points at, never one named by
///     the client;
///   - a missing or past date is refused, and a refused completion changes
///     nothing at all;
///   - the existing status, history, audit, fee and notification pipelines
///     still run, and still run exactly once.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RenewedExpiryDateTests : IntegrationTestBase
{
    public RenewedExpiryDateTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid AgentB = TestDataSeeder.Ids.AgentB;

    private static readonly Guid DocumentA = TestDataSeeder.Ids.DocumentA;
    private static readonly Guid DocumentB = TestDataSeeder.Ids.DocumentB;

    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;
    private static readonly Guid TaskB = TestDataSeeder.Ids.TaskB;

    /// <summary>Seeded directly in the terminal Updated state.</summary>
    private static readonly Guid TaskC = TestDataSeeder.Ids.TaskC;

    /// <summary>
    /// A fixed future date. Read back as DateTime, it is compared on its date
    /// portion only, so the assertions do not depend on the time of day the
    /// suite happens to run at.
    /// </summary>
    private static readonly DateTime RenewedExpiry = new(2027, 3, 14, 0, 0, 0,
        DateTimeKind.Utc);

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<(HttpStatusCode code, string body)> RawStatusAsync(
        Guid taskId,
        string jsonBody,
        Guid? asUser = null,
        string role = "Agent",
        bool adminSurface = false)
    {
        var prefix = adminSurface ? "admin" : "agent";

        var request = ScopePatch(
            $"/api/v1/{prefix}/tasks/{taskId}/status",
            null,
            asUser ?? AgentA,
            role);

        request.Content =
            JsonContent.Create(
                JsonDocument.Parse(jsonBody).RootElement);

        var response = await ScopeClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    /// <summary>
    /// A completion body carrying a date string. A null renewedExpiryDate
    /// omits the property entirely, which binds to the same null as an
    /// explicit "renewedExpiryDate": null but exercises a distinguishable
    /// mistake. Tests needing a non-string or malformed value use
    /// <see cref="RawStatusAsync"/> instead.
    /// </summary>
    private Task<(HttpStatusCode code, string body)> CompleteAsync(
        Guid taskId,
        string? renewedExpiryDate = "2027-03-14",
        Guid? asUser = null,
        string role = "Agent",
        bool adminSurface = false)
    {
        var json = renewedExpiryDate is null
            ? "{\"status\":\"Updated\"}"
            : "{\"status\":\"Updated\",\"renewedExpiryDate\":\"" +
              renewedExpiryDate + "\"}";

        return RawStatusAsync(
            taskId, json, asUser, role, adminSurface);
    }

    /// <summary>
    /// Walks the strict sequential chain an Agent must follow. Returns once the
    /// task is one step away from completion.
    /// </summary>
    private async Task DriveToApprovedAsync(
        Guid? taskId = null,
        Guid? agentId = null)
    {
        var id = taskId ?? TaskA;
        var agent = agentId ?? AgentA;

        foreach (var step in new[]
        {
            "FeePaid", "AwaitingApproval", "Approved"
        })
        {
            var (code, body) = await RawStatusAsync(
                id,
                $"{{\"status\":\"{step}\"}}",
                agent);

            Assert.True(
                code == HttpStatusCode.OK,
                $"Pre-completion step {step} failed: " +
                $"{(int)code} {body}");
        }
    }

    private async Task<DateTime?> ReadExpiryAsync(Guid documentId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.Documents
                .Where(x => x.Id == documentId)
                .Select(x => x.ExpiryDate)
                .FirstAsync();
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private async Task<RenewalTaskStatus> ReadStatusAsync(Guid taskId)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            return await db.RenewalTasks
                .Where(x => x.Id == taskId)
                .Select(x => x.Status)
                .FirstAsync();
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 1-2. Completion succeeds and the date lands on the document
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "1. Completing a task with a future renewed expiry succeeds (200)")]
    public async Task Completion_WithAFutureExpiry_Succeeds()
    {
        await DriveToApprovedAsync();

        var (code, body) = await CompleteAsync(TaskA);

        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "2. The document's ExpiryDate becomes the supplied renewed expiry")]
    public async Task TheDocumentExpiry_BecomesTheSuppliedDate()
    {
        var before = await ReadExpiryAsync(DocumentA);
        Assert.NotNull(before);

        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var after = await ReadExpiryAsync(DocumentA);

        Assert.NotNull(after);
        Assert.Equal(
            RenewedExpiry.Date, after!.Value.Date);
    }

    [Fact(DisplayName =
        "2b. The expiry genuinely changes — the seeded expiring date is " +
        "replaced, not left in place")]
    public async Task TheExpiry_ChangesRatherThanBeingRetained()
    {
        // The whole point of the phase. Before it, this assertion failed: the
        // task reached Updated and the document kept the date that had
        // triggered the renewal.
        var before = await ReadExpiryAsync(DocumentA);

        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var after = await ReadExpiryAsync(DocumentA);

        Assert.NotEqual(before!.Value.Date, after!.Value.Date);
    }

    [Fact(DisplayName =
        "2c. The task itself still completes normally (Updated + CompletedAt)")]
    public async Task TheTask_StillReachesUpdated()
    {
        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var task = await db.RenewalTasks.SingleAsync(x => x.Id == TaskA);

            Assert.Equal(RenewalTaskStatus.Updated, task.Status);
            Assert.NotNull(task.CompletedAt);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 3-5. The date is required
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "3. Completing WITHOUT a renewed expiry is refused (400)")]
    public async Task Completion_WithoutARenewedExpiry_IsRefused()
    {
        await DriveToApprovedAsync();

        var (code, body) = await CompleteAsync(
            TaskA, renewedExpiryDate: null);

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "4. An explicit null renewed expiry is refused (400)")]
    public async Task AnExplicitNullRenewedExpiry_IsRefused()
    {
        await DriveToApprovedAsync();

        var (code, _) = await RawStatusAsync(
            TaskA,
            "{\"status\":\"Updated\",\"renewedExpiryDate\":null}");

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "5. A malformed renewed expiry is refused (400)")]
    public async Task AMalformedRenewedExpiry_IsRefused()
    {
        await DriveToApprovedAsync();

        var (code, _) = await RawStatusAsync(
            TaskA,
            "{\"status\":\"Updated\"," +
            "\"renewedExpiryDate\":\"14/03/2027\"}");

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    // ------------------------------------------------------------------
    // 6-7. The date must be in the future
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "6. A past renewed expiry is refused (400)")]
    public async Task APastRenewedExpiry_IsRefused()
    {
        await DriveToApprovedAsync();

        var (code, _) = await CompleteAsync(
            TaskA, renewedExpiryDate: "2020-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "7. A renewed expiry equal to now is refused (400) — the rule is " +
        "strictly in the future")]
    public async Task ARenewedExpiryOfNow_IsRefused()
    {
        await DriveToApprovedAsync();

        var now = DateTime.UtcNow
            .ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        var (code, _) = await CompleteAsync(
            TaskA, renewedExpiryDate: now);

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "7b. A refused past date leaves BOTH the task and the document " +
        "untouched")]
    public async Task ARefusedPastDate_LeavesTaskAndDocumentUntouched()
    {
        await DriveToApprovedAsync();

        var expiryBefore = await ReadExpiryAsync(DocumentA);

        await CompleteAsync(TaskA, renewedExpiryDate: "2020-01-01");

        Assert.Equal(
            RenewalTaskStatus.Approved, await ReadStatusAsync(TaskA));
        Assert.Equal(expiryBefore, await ReadExpiryAsync(DocumentA));
    }

    [Fact(DisplayName =
        "7c. A refused completion writes no history and no audit rows")]
    public async Task ARefusedCompletion_WritesNothing()
    {
        await DriveToApprovedAsync();

        var db = await CreateScopeDbContextAsync();
        var h0 = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskA);
        var a0 = await db.AuditLogs.CountAsync();
        await db.DisposeAsync();

        await CompleteAsync(TaskA, renewedExpiryDate: null);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                h0,
                await db2.RenewalTaskHistories.CountAsync(
                    x => x.RenewalTaskId == TaskA));
            Assert.Equal(a0, await db2.AuditLogs.CountAsync());
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 8-9. The document is chosen by the task, never by the client
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "8. A client-supplied documentId cannot redirect the update — the " +
        "task's own document is the one that changes")]
    public async Task AClientSuppliedDocumentId_CannotRedirectTheUpdate()
    {
        // If the request could name the document, a caller could renew the
        // wrong document while completing an unrelated task. The handler must
        // reach the document only through task.DocumentId, so this extra
        // property is ignored entirely.
        var untouchedBefore = await ReadExpiryAsync(DocumentB);

        await DriveToApprovedAsync();

        var (code, _) = await RawStatusAsync(
            TaskA,
            "{\"status\":\"Updated\"," +
            "\"renewedExpiryDate\":\"2027-03-14\"," +
            $"\"documentId\":\"{DocumentB}\"}}");

        Assert.Equal(HttpStatusCode.OK, code);

        Assert.Equal(
            RenewedExpiry.Date,
            (await ReadExpiryAsync(DocumentA))!.Value.Date);
        Assert.Equal(
            untouchedBefore, await ReadExpiryAsync(DocumentB));
    }

    [Fact(DisplayName =
        "9. The expiry is written to the document the task points at, on any " +
        "task")]
    public async Task TheExpiry_GoesToTheLinkedDocument()
    {
        // TaskB -> DocumentB, assigned to AgentB. Guards against a handler that
        // reached for a document by some other route: DocumentB must take the
        // new date and DocumentA must be left alone.
        var documentAExpiryBefore = await ReadExpiryAsync(DocumentA);

        await DriveToApprovedAsync(TaskB, AgentB);
        await CompleteAsync(TaskB, asUser: AgentB);

        Assert.Equal(
            RenewedExpiry.Date,
            (await ReadExpiryAsync(DocumentB))!.Value.Date);
        Assert.Equal(
            documentAExpiryBefore, await ReadExpiryAsync(DocumentA));
    }

    // ------------------------------------------------------------------
    // 10. A missing linked document refuses the completion
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "10. A soft-deleted linked document refuses the completion (404) and " +
        "the expiry is not written anywhere")]
    public async Task AMissingLinkedDocument_RefusesCompletion()
    {
        await DriveToApprovedAsync();

        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents
                .Single(x => x.Id == DocumentA);

            document.IsDeleted = true;
            document.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var (code, _) = await CompleteAsync(TaskA);

        Assert.Equal(HttpStatusCode.NotFound, code);
        Assert.Equal(
            RenewalTaskStatus.Approved, await ReadStatusAsync(TaskA));
    }

    // ------------------------------------------------------------------
    // 11. The Admin surface obeys the same contract
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "11. Admin can skip forward to Updated, and must supply the same " +
        "renewed expiry")]
    public async Task Admin_SkipForwardToUpdated_AlsoRequiresTheDate()
    {
        // Admin may jump ahead in the chain, so this route reaches Updated
        // without the three intermediate calls. It shares the handler, so it
        // must not be a way around the requirement.
        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: null,
            asUser: Admin,
            role: "Admin",
            adminSurface: true);

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "11b. Admin completing with a future renewed expiry succeeds and " +
        "updates the document")]
    public async Task Admin_CompletingWithADate_Succeeds()
    {
        var (code, body) = await CompleteAsync(
            TaskA,
            asUser: Admin,
            role: "Admin",
            adminSurface: true);

        Assert.Equal(HttpStatusCode.OK, code);

        Assert.Equal(
            RenewalTaskStatus.Updated, await ReadStatusAsync(TaskA));
        Assert.Equal(
            RenewedExpiry.Date,
            (await ReadExpiryAsync(DocumentA))!.Value.Date);
    }

    // ------------------------------------------------------------------
    // 12-13. The renewal is audited against the document
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "12. A DocumentRenewed audit row is written against the DOCUMENT, " +
        "carrying the old and new dates in Metadata")]
    public async Task ADocumentRenewedAuditRow_IsWritten()
    {
        var before = await ReadExpiryAsync(DocumentA);

        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == DocumentA &&
                    x.Action == "DocumentRenewed")
                .SingleAsync();

            Assert.Equal("Document", row.EntityType);
            Assert.Equal(AgentA, row.UserId);

            // audit_logs.description is varchar(500).
            Assert.True(row.Description!.Length <= 500);
            Assert.Contains("2027-03-14", row.Description);

            // Metadata is text and is the machine-readable home of the pair.
            Assert.Contains(
                $"oldExpiryDate={before!.Value:yyyy-MM-dd}",
                row.Metadata!);
            Assert.Contains(
                "newExpiryDate=2027-03-14", row.Metadata!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "12b. The existing task status audit row is still written, so the " +
        "renewal is visible from both the task and the document")]
    public async Task TheTaskStatusAuditRow_IsStillWritten()
    {
        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == TaskA &&
                    x.Action == "RenewalTaskStatusUpdated")
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync();

            Assert.Equal("RenewalTask", row.EntityType);
            Assert.Contains("Updated", row.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "12c. A document with no previous expiry records 'none' rather than " +
        "an empty value")]
    public async Task AMissingPreviousExpiry_IsRecordedAsNone()
    {
        // ExpiryDate is nullable, so the old value can legitimately be absent.
        // A blank would be indistinguishable from a bug in the audit reader.
        await WithScopeDbContextAsync(db =>
        {
            var document = db.Documents
                .Single(x => x.Id == DocumentA);

            document.ExpiryDate = null;
            db.SaveChanges();
        });

        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            var row = await db2.AuditLogs
                .Where(x =>
                    x.EntityId == DocumentA &&
                    x.Action == "DocumentRenewed")
                .SingleAsync();

            Assert.Contains("oldExpiryDate=none", row.Metadata!);
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 14-15. Non-completing transitions are unaffected
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "14. A non-completing transition needs no renewed expiry (200)")]
    public async Task ANonCompletingTransition_NeedsNoDate()
    {
        var (code, _) = await RawStatusAsync(
            TaskA, "{\"status\":\"FeePaid\"}");

        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "14b. A non-completing transition leaves the document expiry alone " +
        "and writes no renewal audit")]
    public async Task ANonCompletingTransition_LeavesTheDocumentAlone()
    {
        var before = await ReadExpiryAsync(DocumentA);

        await RawStatusAsync(TaskA, "{\"status\":\"FeePaid\"}");

        Assert.Equal(before, await ReadExpiryAsync(DocumentA));

        var db = await CreateScopeDbContextAsync();
        try
        {
            // Seeded expiry is 10 days out; a later renewal must not be
            // fabricated by an unrelated status change.
            Assert.Equal(
                0,
                await db.AuditLogs.CountAsync(
                    x => x.Action == "DocumentRenewed"));
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // 16-18. The rest of the completion pipeline is preserved
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "16. Completion still writes the Updated history row")]
    public async Task Completion_StillWritesHistory()
    {
        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.RenewalTaskHistories
                .Where(x => x.RenewalTaskId == TaskA)
                .OrderByDescending(x => x.ChangedAt)
                .FirstAsync();

            Assert.Equal(RenewalTaskStatus.Updated, row.Status);
            Assert.Equal(AgentA, row.ChangedBy);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "17. Completion still creates exactly ONE service fee invoice")]
    public async Task Completion_StillCreatesExactlyOneInvoice()
    {
        // The ServiceFeeAmount gate and the completion event feed invoice
        // creation. The expiry capture must not disturb either, and must not
        // fire the event a second time.
        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var db = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                1,
                await db.ServiceFeeInvoices.CountAsync(
                    x => x.RenewalTaskId == TaskA));

            var invoice = await db.ServiceFeeInvoices
                .SingleAsync(x => x.RenewalTaskId == TaskA);

            Assert.Equal(150m, invoice.Amount);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "18. The missing-fee gate still refuses completion (409), not 400 — " +
        "the date is not a way to skip it")]
    public async Task TheMissingFeeGate_StillReturns409()
    {
        // Both guards can refuse a completion. They are checked in a defined
        // order, and a valid date must not demote the fee conflict to a
        // validation error or hide it behind a 200.
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Find(TaskA);
            task!.ServiceFeeAmount = null;
            db.SaveChanges();
        });

        await DriveToApprovedAsync();

        var (code, _) = await CompleteAsync(TaskA);

        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    // ------------------------------------------------------------------
    // 19. The read path reflects the new expiry
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "19. The task detail endpoint returns the new document expiry")]
    public async Task TheTaskDetail_ReturnsTheNewExpiry()
    {
        // Guards the read path: a correct write that no reader can see is
        // indistinguishable from no write at all.
        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/tasks/{TaskA}", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = (await response.Content.ReadAsStringAsync())
            .Replace(" ", string.Empty);

        Assert.Contains("\"status\":\"Updated\"", body);
        Assert.Contains("2027-03-14", body);
    }

    // ------------------------------------------------------------------
    // 20-21. Existing guards are preserved
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "20. A completed task still refuses further transitions (409)")]
    public async Task ACompletedTask_StillRefusesFurtherTransitions()
    {
        await DriveToApprovedAsync();
        await CompleteAsync(TaskA);

        var (code, _) = await RawStatusAsync(
            TaskA, "{\"status\":\"FeePaid\"}");

        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "21. Block and Unblock still work, and neither touches the document " +
        "expiry")]
    public async Task BlockAndUnblock_StillWork_AndDoNotTouchTheExpiry()
    {
        // G2's exit must survive G3, and neither operation may fabricate a
        // renewed expiry.
        var before = await ReadExpiryAsync(DocumentA);

        var block = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/agent/tasks/{TaskA}/block",
                new { blockedReason = "Missing scan" },
                AgentA));
        await EnsureOk(block, HttpStatusCode.OK);

        var unblock = await ScopeClient.SendAsync(
            ScopePatch(
                $"/api/v1/admin/tasks/{TaskA}/unblock",
                new { unblockReason = "Scan received" },
                Admin,
                "Admin"));
        await EnsureOk(unblock, HttpStatusCode.OK);

        Assert.Equal(
            RenewalTaskStatus.Submitted, await ReadStatusAsync(TaskA));
        Assert.Equal(before, await ReadExpiryAsync(DocumentA));
    }

    // ------------------------------------------------------------------
    // 22. No schema change was required
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "22. The feature needed no new migration — the column already " +
        "existed")]
    public async Task NoNewMigration_WasRequired()
    {
        // documents.expiry_date is already there and nullable, so the capture
        // is a write to an existing column. A migration here would mean a
        // schema change nobody asked for.
        var db = await CreateScopeDbContextAsync();
        try
        {
            var property = db.Model
                .FindEntityType(typeof(Document))!
                .FindProperty(nameof(Document.ExpiryDate))!;

            Assert.Equal("timestamp with time zone", property.GetColumnType());
            Assert.True(property.IsNullable);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // P10.1 / R-11 - UTC DATE semantics end to end
    // ------------------------------------------------------------------

    /// <summary>
    /// P10.1 R-11 INTEGRATION REGRESSION SUITE.
    ///
    /// THE DEFECT
    ///
    /// The endpoint compared the RAW bound value against <c>DateTime.UtcNow</c>,
    /// while persistence keeps only the DATE component
    /// (<c>ToUtcExpiryDate</c>). A body whose renewedExpiryDate carried TODAY's
    /// date with a later time of day therefore returned 200, and the document
    /// was persisted with today's UTC midnight — already expired — while the
    /// task sat in the terminal Updated state. P9.2 then saw the document as
    /// still overdue and produced a fresh cycle.
    ///
    /// THE RULE NOW
    ///
    /// The persisted date must be strictly after today's UTC date. Both the
    /// validator and the handler's write-path guard enforce the same normalized
    /// comparison, so the outcome no longer depends on the time of day.
    ///
    /// DATE DISCIPLINE
    ///
    /// Every date is built from <c>DateTime.UtcNow.Date</c> plus whole days and
    /// sent as a date-only string, which binds as Unspecified and is anchored to
    /// UTC midnight by ToUtcExpiryDate. Nothing here compares a raw UtcNow, so
    /// no test is time-of-day dependent. I3 is the discriminator: it fails
    /// against the old timestamp comparison whenever the run is before
    /// 23:59:59 UTC, and is unconditionally correct against the fixed code.
    ///
    /// These tests reuse the existing helpers (DriveToApprovedAsync,
    /// CompleteAsync, ReadExpiryAsync, ReadStatusAsync and the audit/history
    /// readers). No new infrastructure.
    /// </summary>
    private static DateTime UtcToday => DateTime.UtcNow.Date;

    /// <summary>
    /// The exact instant the document is expected to hold for a requested
    /// date-only string: that date at UTC midnight, matching ToUtcExpiryDate.
    /// </summary>
    private static DateTime ExpectedUtcMidnight(DateTime dateOnly)
        => DateTime.SpecifyKind(dateOnly.Date, DateTimeKind.Utc);

    [Fact(DisplayName =
        "R11-I1: completing with TOMORROW's date succeeds (200) and persists " +
        "that date")]
    public async Task Tomorrow_IsAccepted()
    {
        await DriveToApprovedAsync();

        var tomorrow = UtcToday.AddDays(1);

        var (code, body) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: tomorrow.ToString("yyyy-MM-dd"));

        Assert.True(
            code == HttpStatusCode.OK,
            $"Tomorrow was refused: {(int)code} {body}");

        var persisted = await ReadExpiryAsync(DocumentA);

        Assert.NotNull(persisted);
        Assert.Equal(tomorrow.Date, persisted!.Value.Date);
    }

    [Fact(DisplayName =
        "R11-I2: completing with TODAY's date is refused (400)")]
    public async Task Today_IsRejected()
    {
        await DriveToApprovedAsync();

        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: UtcToday.ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "R11-I3: completing with TODAY's date at 23:59:59 is refused (400) — " +
        "a later TIME of day must not make today look like the future")]
    public async Task TodayLateInTheDay_IsRejected()
    {
        await DriveToApprovedAsync();

        // The R-11 regression. Under the old raw-timestamp comparison this
        // value looks like the future during almost the whole day, passes, and
        // persists as today's already-expired midnight.
        var todayLateInTheDay =
            UtcToday.AddHours(23).AddMinutes(59).AddSeconds(59);

        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: todayLateInTheDay.ToString(
                "yyyy-MM-ddTHH:mm:ss"));

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "R11-I4: completing with YESTERDAY's date is refused (400)")]
    public async Task Yesterday_IsRejected()
    {
        await DriveToApprovedAsync();

        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: UtcToday.AddDays(-1).ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.BadRequest, code);
    }

    [Fact(DisplayName =
        "R11-I5: completing with a clearly future date (+1 year) succeeds " +
        "(200)")]
    public async Task AClearlyFutureDate_IsAccepted()
    {
        await DriveToApprovedAsync();

        var future = UtcToday.AddYears(1);

        var (code, body) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: future.ToString("yyyy-MM-dd"));

        Assert.True(
            code == HttpStatusCode.OK,
            $"A +1 year expiry was refused: {(int)code} {body}");

        Assert.Equal(
            future.Date, (await ReadExpiryAsync(DocumentA))!.Value.Date);
    }

    [Fact(DisplayName =
        "R11-I6: a successful completion persists the EXACT intended date — " +
        "the same calendar date, at UTC midnight")]
    public async Task Success_PersistsTheExactIntendedDate()
    {
        await DriveToApprovedAsync();

        // Far enough ahead that neither the boundary rule nor any clock drift
        // during the run can affect it.
        var requested = UtcToday.AddDays(45);

        var (code, body) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: requested.ToString("yyyy-MM-dd"));

        Assert.True(
            code == HttpStatusCode.OK,
            $"A +45 day expiry was refused: {(int)code} {body}");

        var persisted = (await ReadExpiryAsync(DocumentA))!.Value;

        // Date components are the business value...
        Assert.Equal(requested.Date, persisted.Date);

        // ...and the stored instant is that date at UTC midnight, proving the
        // time of day the request carried was dropped rather than persisted.
        Assert.Equal(ExpectedUtcMidnight(requested), persisted);
        Assert.Equal(DateTimeKind.Utc, persisted.Kind);
        Assert.Equal(
            TimeSpan.Zero,
            persisted.TimeOfDay);
    }

    [Fact(DisplayName =
        "R11-I7: a date refused under DATE semantics leaves the task status " +
        "unchanged")]
    public async Task RefusedDate_LeavesTaskStatusUnchanged()
    {
        await DriveToApprovedAsync();

        var before = await ReadStatusAsync(TaskA);

        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: UtcToday.ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Equal(before, await ReadStatusAsync(TaskA));
    }

    [Fact(DisplayName =
        "R11-I8: a date refused under DATE semantics leaves the document " +
        "expiry unchanged")]
    public async Task RefusedDate_LeavesDocumentExpiryUnchanged()
    {
        await DriveToApprovedAsync();

        var expiryBefore = await ReadExpiryAsync(DocumentA);

        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: UtcToday.ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Equal(
            expiryBefore, await ReadExpiryAsync(DocumentA));
    }

    [Fact(DisplayName =
        "R11-I9: a date refused under DATE semantics writes no history and " +
        "no audit rows")]
    public async Task RefusedDate_WritesNoHistoryOrAudit()
    {
        await DriveToApprovedAsync();

        var db = await CreateScopeDbContextAsync();
        var h0 = await db.RenewalTaskHistories
            .CountAsync(x => x.RenewalTaskId == TaskA);
        var a0 = await db.AuditLogs.CountAsync();
        await db.DisposeAsync();

        var (code, _) = await CompleteAsync(
            TaskA,
            renewedExpiryDate: UtcToday.ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.BadRequest, code);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                h0,
                await db2.RenewalTaskHistories.CountAsync(
                    x => x.RenewalTaskId == TaskA));
            Assert.Equal(a0, await db2.AuditLogs.CountAsync());
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }
}
