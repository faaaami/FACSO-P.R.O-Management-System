using System.Net;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Staff;

/// <summary>
/// P10.3 - locks the authoritative staff workload semantics that survive the
/// removal of the dead /dashboard/staff-workload endpoint.
///
/// SOURCE OF TRUTH: GET /api/v1/admin/staff/workload, implemented by
/// RenewalTaskReadRepository.GetAllStaffWorkloadAsync. This file does not
/// change that implementation; it pins the behaviour it already has.
///
/// THE ENUM
///
///   RenewalTaskStatus: Submitted=1 FeePaid=2 AwaitingApproval=3
///                      Blocked=4 Approved=5 Updated=6
///
/// THE CONTRACT
///
///   ActiveTaskCount  = Submitted + FeePaid + AwaitingApproval + Approved (1,2,3,5)
///   BlockedTaskCount = Blocked (4)  - and nothing else
///   Updated (6)      = terminal/completed. Counts as NEITHER active nor blocked.
///                      This is the distinction the deleted endpoint got wrong by
///                      labelling status 6 as "BlockedTasks".
///   TasksByStatus    = all six keys always present; absent statuses are 0.
///
/// THE SCOPE
///
///   assigned-to-staff only; task IsDeleted = false; staff role Agent and
///   staff IsDeleted = false; LEFT JOIN so zero-task agents remain visible.
///
/// TEST-DATA DISCIPLINE
///
/// The shared seed assigns TaskA/TaskC to Agent A and TaskB/TaskD to Agent B,
/// which would make every expectation a delta against an unrelated baseline.
/// Each test therefore UNASSIGNS every seeded task first (an UPDATE, not a
/// delete, so the service-fee and gov-fee invoices that reference those tasks
/// stay valid) and then plants exactly the rows it describes. Because workload
/// is keyed on AssignedStaffId, unassigned tasks are invisible to every agent,
/// which makes the counts absolute rather than relative.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StaffWorkloadSemanticsTests : IntegrationTestBase
{
    public StaffWorkloadSemanticsTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid AgentB = TestDataSeeder.Ids.AgentB;

    private static readonly string[] AllStatusKeys =
    {
        "Submitted", "FeePaid", "AwaitingApproval",
        "Blocked", "Approved", "Updated"
    };

    // ==============================================================
    // Read / seed helpers
    // ==============================================================

    private async Task<GetAllStaffWorkloadResponseDto> ReadWorkloadAsync()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/admin/staff/workload", Admin, "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetAllStaffWorkloadResponseDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<GetAllStaffWorkloadResponseDto>(
            envelope!.Data);
    }

    private static GetStaffWorkloadResponseDto RowFor(
        GetAllStaffWorkloadResponseDto payload,
        Guid staffId)
        => payload.Items.Single(x => x.StaffId == staffId);

    /// <summary>
    /// Unassigns every seeded renewal task. This is an UPDATE rather than a
    /// delete so that seeded invoices referencing those tasks stay intact, and
    /// it is sufficient because workload is keyed on AssignedStaffId.
    /// </summary>
    private async Task UnassignAllSeededTasksAsync()
    {
        await WithScopeDbContextAsync(db =>
        {
            foreach (var task in db.RenewalTasks.ToList())
            {
                task.AssignedStaffId = null;
                task.UpdatedAt = DateTime.UtcNow;
            }

            db.SaveChanges();
        });
    }

    /// <summary>
    /// Plants one renewal task on a FRESH document. renewal_tasks carries a
    /// UNIQUE index on DocumentId (the live-task slot introduced with the
    /// document-uniqueness work), so a probe can never reuse DocumentA or B.
    /// </summary>
    private static void AddAssignedTask(
        IApplicationDbContext db,
        Guid? agentId,
        RenewalTaskStatus status,
        bool isDeleted = false)
    {
        var now = DateTime.UtcNow;
        var documentId = Guid.NewGuid();

        db.Documents.Add(new Document
        {
            Id = documentId,
            ClientEntityId = TestDataSeeder.Ids.EntityA,
            EmployeeId = null,
            Type = DocumentType.Visa,
            DocumentNumber = $"P103-{documentId:N}"[..20],
            IssueDate = now.AddYears(-1),
            ExpiryDate = now.AddDays(300),
            FileUrl = $"https://storage.test/{documentId:N}",
            FileName = "p103-probe.pdf",
            ContentType = "application/pdf",
            FileSize = 2048,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });

        db.RenewalTasks.Add(new RenewalTask
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            ClientCompanyId = TestDataSeeder.Ids.CompanyA,
            AssignedStaffId = agentId,
            Status = status,
            ServiceFeeAmount = 150m,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? now : null
        });
    }

    // ==============================================================
    // T3 - an active assigned task
    // ==============================================================

    [Fact(DisplayName =
        "T3: one active (Submitted) assigned task gives ActiveTaskCount=1 " +
        "and BlockedTaskCount=0")]
    public async Task OneActiveAssignedTask_IsActiveAndNotBlocked()
    {
        await UnassignAllSeededTasksAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddAssignedTask(db, AgentA, RenewalTaskStatus.Submitted);

            db.SaveChanges();
        });

        var row = RowFor(await ReadWorkloadAsync(), AgentA);

        Assert.Equal(1, row.ActiveTaskCount);
        Assert.Equal(0, row.BlockedTaskCount);
        Assert.Equal(1, row.TasksByStatus["Submitted"]);
    }

    // ==============================================================
    // T4 - a Blocked task  (the key discriminator)
    // ==============================================================

    [Fact(DisplayName =
        "T4: one Blocked task gives BlockedTaskCount=1 and " +
        "ActiveTaskCount=0")]
    public async Task OneBlockedTask_IsBlockedAndNotActive()
    {
        await UnassignAllSeededTasksAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddAssignedTask(db, AgentA, RenewalTaskStatus.Blocked);

            db.SaveChanges();
        });

        var row = RowFor(await ReadWorkloadAsync(), AgentA);

        Assert.Equal(1, row.BlockedTaskCount);
        Assert.Equal(0, row.ActiveTaskCount);
        Assert.Equal(1, row.TasksByStatus["Blocked"]);
    }

    // ==============================================================
    // T5 - an Updated task is terminal  (the key discriminator)
    // ==============================================================

    [Fact(DisplayName =
        "T5: one Updated (terminal/completed) task counts as neither active " +
        "nor blocked")]
    public async Task OneUpdatedTask_IsNeitherActiveNorBlocked()
    {
        await UnassignAllSeededTasksAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddAssignedTask(db, AgentA, RenewalTaskStatus.Updated);

            db.SaveChanges();
        });

        var row = RowFor(await ReadWorkloadAsync(), AgentA);

        // Updated is completed work. Reporting it as blocked - which is what
        // the deleted dashboard endpoint did - is exactly the defect this
        // suite exists to prevent.
        Assert.Equal(0, row.BlockedTaskCount);
        Assert.Equal(0, row.ActiveTaskCount);
        Assert.Equal(1, row.TasksByStatus["Updated"]);
    }

    // ==============================================================
    // T6 - zero-task agents stay visible (LEFT JOIN)
    // ==============================================================

    [Fact(DisplayName =
        "T6: a zero-task Agent is still listed with all six status keys " +
        "present and every count zero")]
    public async Task ZeroTaskAgent_IsListedWithAllZeroCounts()
    {
        await UnassignAllSeededTasksAsync();

        var row = RowFor(await ReadWorkloadAsync(), AgentB);

        Assert.Equal(0, row.ActiveTaskCount);
        Assert.Equal(0, row.BlockedTaskCount);

        // All six keys must be present, not merely reachable. The frontend
        // renders one tile per key and treats an unknown key as zero, so a
        // missing key would be silently indistinguishable from a real zero.
        Assert.Equal(AllStatusKeys.Length, row.TasksByStatus.Count);

        foreach (var key in AllStatusKeys)
        {
            Assert.True(
                row.TasksByStatus.ContainsKey(key),
                $"tasksByStatus is missing '{key}'");
            Assert.Equal(0, row.TasksByStatus[key]);
        }
    }

    // ==============================================================
    // T7 - soft-deleted staff are excluded
    // ==============================================================

    [Fact(DisplayName =
        "T7: a soft-deleted Agent is excluded from the workload results")]
    public async Task SoftDeletedAgent_IsExcluded()
    {
        await UnassignAllSeededTasksAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddAssignedTask(db, AgentB, RenewalTaskStatus.Submitted);

            var agentB = db.Users.Single(
                x => x.Id == AgentB);

            agentB.IsDeleted = true;
            agentB.DeletedAt = DateTime.UtcNow;

            db.SaveChanges();
        });

        var payload = await ReadWorkloadAsync();

        Assert.DoesNotContain(
            payload.Items, x => x.StaffId == AgentB);

        Assert.Contains(
            payload.Items, x => x.StaffId == AgentA);
    }

    // ==============================================================
    // T8 - unassigned tasks are outside staff workload
    // ==============================================================

    [Fact(DisplayName =
        "T8: an unassigned task does not appear in any agent's workload")]
    public async Task UnassignedTask_IsExcludedFromWorkload()
    {
        await UnassignAllSeededTasksAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddAssignedTask(db, null, RenewalTaskStatus.Submitted);

            // The four seeded tasks are unassigned by the reset above, so any
            // inflation here would have to come from one of the five.
            db.SaveChanges();
        });

        var payload = await ReadWorkloadAsync();

        foreach (var row in payload.Items)
        {
            Assert.Equal(0, row.ActiveTaskCount);
            Assert.Equal(0, row.BlockedTaskCount);
        }
    }

    // ==============================================================
    // T9 - soft-deleted tasks are excluded
    // ==============================================================

    [Fact(DisplayName =
        "T9: a soft-deleted assigned task is excluded from workload counts")]
    public async Task SoftDeletedTask_IsExcluded()
    {
        await UnassignAllSeededTasksAsync();

        await WithScopeDbContextAsync(db =>
        {
            AddAssignedTask(
                db, AgentA, RenewalTaskStatus.Submitted,
                isDeleted: true);

            db.SaveChanges();
        });

        var row = RowFor(await ReadWorkloadAsync(), AgentA);

        Assert.Equal(0, row.ActiveTaskCount);
        Assert.Equal(0, row.BlockedTaskCount);
        Assert.Equal(0, row.TasksByStatus["Submitted"]);
    }
}
