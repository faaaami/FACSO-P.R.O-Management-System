using System.Net;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Staff;

/// <summary>
/// P10.3 - removal of the dead legacy dashboard staff-workload endpoint.
///
/// GET /api/v1/dashboard/staff-workload was a second, incorrect implementation
/// of staff workload that nothing consumed. Its SQL mapped the renewal-task
/// statuses against a stale ordinal assumption:
///
///   PendingTasks    IN (1,2,3)  Submitted / FeePaid / AwaitingApproval  (correct)
///   InProgressTasks = 5          Approved
///   BlockedTasks    = 6          Updated  <-- terminal/completed, NOT blocked
///
/// and the real Blocked status (4) appeared in no column at all. It also used
/// an INNER JOIN, so zero-task agents disappeared from the response entirely.
///
/// These tests pin the removal. The surviving, authoritative implementation is
/// /api/v1/admin/staff/workload in RenewalTaskReadRepository, which is covered
/// by StaffWorkloadSemanticsTests.
///
/// T1 is the discriminator: it fails while the route is still mapped (200) and
/// passes only once the route is gone (404).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StaffWorkloadEndpointRemovalTests : IntegrationTestBase
{
    public StaffWorkloadEndpointRemovalTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;

    [Fact(DisplayName =
        "T1: GET /dashboard/staff-workload returns 404 - the dead legacy " +
        "endpoint is no longer exposed")]
    public async Task LegacyDashboardStaffWorkloadRoute_IsGone()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/dashboard/staff-workload", Admin, "Admin"));

        // Asserted, not EnsureOk'd: the route must not resolve at all now.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "T2: GET /dashboard/summary still returns 200 - removing the " +
        "workload action did not break DashboardController")]
    public async Task DashboardSummary_StillResolves()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/dashboard/summary", Admin, "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<object>>(response);

        Assert.NotNull(envelope);
    }
}
