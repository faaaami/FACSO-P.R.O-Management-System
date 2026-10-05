using System.Net;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Status;

/// <summary>
/// Verifies the renewal-task status machine exactly: strictly sequential
/// agent transitions, Blocked only via the block endpoint, terminal Updated
/// immovable, ServiceFeeAmount required to complete, and ownership 404.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StatusMachineTests : IntegrationTestBase
{
    public StatusMachineTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;
    private static readonly Guid TaskB = TestDataSeeder.Ids.TaskB;

    /// <summary>
    /// A renewed document expiry is required to complete a task, so this
    /// helper supplies a future one whenever the target status is Updated.
    /// Tests that exercise that requirement itself send raw bodies instead.
    /// </summary>
    private static readonly DateTime FutureExpiry = DateTime.UtcNow.AddYears(1);

    private async Task<(HttpStatusCode code, string body)> PatchStatusAsync(
        Guid taskId,
        string status,
        string? note = null,
        Guid? asUser = null,
        string role = "Agent")
    {
        object payload = new { status, note };

        if (string.Equals(status, "Updated", StringComparison.OrdinalIgnoreCase))
        {
            payload = new { status, note, renewedExpiryDate = FutureExpiry };
        }

        var request = ScopePatch(
            $"/api/v1/agent/tasks/{taskId}/status",
            payload,
            asUser ?? AgentA,
            role);

        var response = await ScopeClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    [Fact(DisplayName =
        "Submitted -> FeePaid is allowed (200)")]
    public async Task Submitted_To_FeePaid_IsAllowed()
    {
        var (code, _) = await PatchStatusAsync(TaskA, "FeePaid");
        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "FeePaid -> AwaitingApproval follows the sequential chain (200)")]
    public async Task FeePaid_To_AwaitingApproval_IsAllowed()
    {
        await PatchStatusAsync(TaskA, "FeePaid");
        var (code, _) = await PatchStatusAsync(TaskA, "AwaitingApproval");
        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact(DisplayName =
        "Skipping a status is rejected (409, e.g. Submitted -> AwaitingApproval)")]
    public async Task SkippingStatus_Returns409()
    {
        var (code, _) = await PatchStatusAsync(TaskA, "AwaitingApproval");
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "Re-transitioning to the current status is rejected (409)")]
    public async Task SameStatus_Returns409()
    {
        var (code, _) = await PatchStatusAsync(TaskA, "Submitted");
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "Backward transition is rejected (409)")]
    public async Task BackwardTransition_Returns409()
    {
        await PatchStatusAsync(TaskA, "FeePaid");
        var (code, _) = await PatchStatusAsync(TaskA, "Submitted");
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "Blocked cannot be set via the status endpoint (409)")]
    public async Task BlockedViaStatusEndpoint_Returns409()
    {
        var (code, _) = await PatchStatusAsync(TaskA, "Blocked");
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "Block endpoint sets Blocked from a non-terminal state (200)")]
    public async Task Block_FromSubmitted_Returns200()
    {
        var request = ScopePatch(
            $"/api/v1/agent/tasks/{TaskA}/block",
            new { blockedReason = "Missing file" },
            AgentA);

        var response = await ScopeClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "A valid sequential run through to Updated succeeds (200 each)")]
    public async Task FullSequentialAdvance_ToUpdated_Succeeds()
    {
        var steps = new[]
        {
            "FeePaid",
            "AwaitingApproval",
            "Approved",
            "Updated"
        };

        foreach (var step in steps)
        {
            var (code, _) = await PatchStatusAsync(TaskA, step);
            Assert.Equal(HttpStatusCode.OK, code);
        }
    }

    [Fact(DisplayName =
        "Updated is terminal: further transition returns 409")]
    public async Task Updated_IsTerminal_ForTransitions()
    {
        foreach (var step in new[] { "FeePaid", "AwaitingApproval", "Approved", "Updated" })
        {
            await PatchStatusAsync(TaskA, step);
        }

        var (code, _) = await PatchStatusAsync(TaskA, "FeePaid");
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "Blocked cannot be set once the task is terminal Updated (409)")]
    public async Task Block_OnUpdated_Returns409()
    {
        foreach (var step in new[] { "FeePaid", "AwaitingApproval", "Approved", "Updated" })
        {
            await PatchStatusAsync(TaskA, step);
        }

        var request = ScopePatch(
            $"/api/v1/agent/tasks/{TaskA}/block",
            new { blockedReason = "Too late" },
            AgentA);

        var response = await ScopeClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName =
        "Completing (Updated) without a ServiceFeeAmount returns 409")]
    public async Task UpdatedWithoutServiceFee_Returns409()
    {
        // Remove the seeded service fee for this task.
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Find(TestDataSeeder.Ids.TaskA);
            task!.ServiceFeeAmount = null;
            db.SaveChanges();
        });

        foreach (var step in new[] { "FeePaid", "AwaitingApproval", "Approved" })
        {
            await PatchStatusAsync(TaskA, step);
        }

        var (code, _) = await PatchStatusAsync(TaskA, "Updated");
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact(DisplayName =
        "Client role cannot change task status (403)")]
    public async Task ClientRoleChangingStatus_Returns403()
    {
        var (code, _) = await PatchStatusAsync(
            TaskA, "FeePaid",
            asUser: TestDataSeeder.Ids.ClientUserA,
            role: "Client");
        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "Agent cannot change a task assigned to another agent (404)")]
    public async Task UpdateOtherAgentsTask_Returns404()
    {
        var (code, _) = await PatchStatusAsync(TaskB, "FeePaid");
        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact(DisplayName =
        "Each status change records a history row")]
    public async Task StatusChanges_RecordHistoryRows()
    {
        await PatchStatusAsync(TaskA, "FeePaid");
        await PatchStatusAsync(TaskA, "AwaitingApproval");

        var response = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/agent/tasks/{TaskA}/history", AgentA));

        await EnsureOk(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("FeePaid", body);
        Assert.Contains("AwaitingApproval", body);
    }

    [Fact(DisplayName =
        "Admin role on the agent status surface returns 403 (C1 gate; " +
        "admin-skip lives on the Admin tasks surface)")]
    public async Task Admin_OnAgentStatusEndpoint_Returns403()
    {
        var (code, _) = await PatchStatusAsync(
            TaskA, "Approved",
            asUser: TestDataSeeder.Ids.Admin,
            role: "Admin");
        Assert.Equal(HttpStatusCode.Forbidden, code);
    }
}
