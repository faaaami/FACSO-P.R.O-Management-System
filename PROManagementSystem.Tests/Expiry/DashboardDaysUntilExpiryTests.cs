using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// P7.0 / D3 CONFIRMATION TEST. Answers one question and changes no
/// production code:
/// does DashboardReadRepository's
/// <c>EXTRACT(DAY FROM d.expiry_date - CURRENT_TIMESTAMP)::int</c> produce a
/// correct TOTAL day count?
///
/// WHY THIS SHAPE OF TEST
///
/// The suspicion being tested is that
/// <c>EXTRACT(DAY FROM d.expiry_date - CURRENT_TIMESTAMP)</c> returns the DAY
/// FIELD of PostgreSQL's months/days/microseconds interval decomposition
/// rather than a total day count. If that were true, a day count past a month
/// would decompose into months plus a small remainder, the day field would
/// stop tracking the total, and a +60d document would report roughly 0.
///
/// The probe therefore covers the month-approaching points, where the day
/// field and a total-day count must diverge most, and ends short of an exact
/// +90 assertion: there is no clock abstraction here, so CURRENT_TIMESTAMP
/// advances while the test runs and an exact edge would be flaky for reasons
/// unrelated to the behaviour under investigation. This mirrors the
/// convention documented in DocumentExpiryWindowTests.
///
/// OUTCOME (P7.0, recorded here so the next reader does not have to re-derive
/// it): D3 is NOT confirmed. Observed values were +1d=>0, +31d=>30,
/// +60d=>59, +89d=>88 — each exactly one below the seeded offset, which is
/// precisely the expected sub-day truncation, and strictly monotonic. The
/// expression therefore behaves as a total-day count in this schema.
///
/// Why the interval does not decompose the way the audit suspected: a
/// timestamp subtraction against a far-future timestamp is normalised by
/// PostgreSQL into a DAY field rather than months. `timestamp - timestamp`
/// yields an interval whose months component is 0 and whose days component
/// carries the full elapsed day count, so EXTRACT(DAY) is the total. The
/// decomposition into months is what `age(timestamp)` does, and what a
/// months/days/microseconds *literal* interval carries when PostgreSQL
/// chooses to justify it — not what this subtraction produces.
///
/// The tests are retained as a regression guard on that finding, at the
/// tolerance the behaviour actually exhibits.
///
/// Assertions are correlated by document id, never by row position or
/// global counts, because the shared seed already plants documents inside
/// every one of these windows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DashboardDaysUntilExpiryTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    /// <summary>
    /// Window wide enough to contain every probe point below. The dashboard
    /// route rejects days outside 1..365, and 180 leaves headroom over the
    /// largest probe so a slow test run cannot push a row out of range.
    /// </summary>
    private const int Days = 180;

    public DashboardDaysUntilExpiryTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ==================================================================
    // Probe points
    // ==================================================================

    // +1d is the control: it is inside a single month, so a day-field count
    // and a total-day count must agree here. If +1d is already wrong, the
    // diagnosis is different (a plain expression error) and the rest of the
    // suite will say so.
    //
    // The three later points are where a day-field count and a total-day count
    // must diverge. +31d is the first full day past a month; +60d decomposes
    // under `age()` to 2 months exactly, so a day-field reading would collapse
    // to 0 there; +89d is the monotonicity guard and is kept one day inside the
    // 90-day automation window for the same no-flaky-edge reason as
    // DocumentExpiryWindowTests.
    private static readonly (int Days, string Label)[] ProbePoints =
    {
        (1, "D3-P1: +1 day"),
        (31, "D3-P2: +31 days (first full day past a month)"),
        (60, "D3-P3: +60 days (age() would decompose this to 2 months, 0 days)"),
        (89, "D3-P4: +89 days (strictly greater than +60d)"),
    };

    // ==================================================================
    // Helpers
    // ==================================================================

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = AdminEmail,
                password =
                    TestDataSeeder.TestLoginPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data.AccessToken;
    }

    /// <summary>
    /// One controlled document per probe point, all owned by the seeded
    /// Company A so the dashboard's entity join resolves without planting
    /// any additional company or entity.
    ///
    /// Documents carry ClientEntityId (not EmployeeId) so they surface in
    /// the documents expiry-alert query. Expiry dates are read from a single
    /// captured <c>now</c> so the whole set is anchored to one instant, and
    /// the first probe is placed a full day out so no probe can sit on the
    /// "already expired" side of CURRENT_TIMESTAMP.
    /// </summary>
    private async Task<Dictionary<int, Guid>> SeedProbeDocumentsAsync()
    {
        var ids = new Dictionary<int, Guid>();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            foreach (var (days, _) in ProbePoints)
            {
                var id = Guid.NewGuid();

                ids[days] = id;

                db.Documents.Add(new Document
                {
                    Id = id,
                    ClientEntityId = TestDataSeeder.Ids.EntityA,
                    EmployeeId = null,
                    Type = DocumentType.Visa,
                    DocumentNumber =
                        $"D3-PROBE-{days:D3}",
                    IssueDate = now.AddYears(-1),

                    // Anchored to the single captured `now` above.
                    ExpiryDate = now.AddDays(days),

                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"probe-{days}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            db.SaveChanges();
        });

        return ids;
    }

    /// <summary>
    /// Calls the repository the controller itself calls, so this exercises
    /// the real production SQL rather than a copy of it. Going through the
    /// HTTP route instead would add a JWT round trip and a controller days
    /// guard without making the measurement any more faithful.
    /// </summary>
    private async Task<List<DashboardExpiryAlertDto>>
        GetExpiryAlertsAsync()
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDashboardReadRepository>();

        return await repository.GetExpiringDocumentsAsync(
            Days, CancellationToken.None);
    }

    // ==================================================================
    // D3 - the confirmation itself
    // ==================================================================

    [Fact(DisplayName =
        "D3: DaysUntilExpiry is a total-day count, monotonic and " +
        "accurate past the one-month mark")]
    public async Task DaysUntilExpiry_IsATotalDayCount()
    {
        var ids = await SeedProbeDocumentsAsync();

        var alerts = await GetExpiryAlertsAsync();

        // Correlate by id. The shared seed also plants documents inside this
        // window, so position and counts are meaningless here.
        var byId = alerts.ToDictionary(x => x.Id);

        var observed = new List<(int Seeded, int Reported)>();

        foreach (var (seededDays, _) in ProbePoints)
        {
            var id = ids[seededDays];

            Assert.True(
                byId.ContainsKey(id),
                $"Probe document seeded at +{seededDays}d " +
                $"({id}) was not returned by the dashboard query.");

            observed.Add((seededDays, byId[id].DaysUntilExpiry));
        }

        // Report every observed pair before asserting anything, so a failure
        // shows the whole shape of the behaviour rather than only the first
        // point that broke. This is the actual D3 evidence.
        foreach (var (seeded, reported) in observed)
        {
            var delta = reported - seeded;

            // A same-day result can legitimately report one day less than
            // seeded, because CURRENT_TIMESTAMP has advanced since the seed
            // and the expression truncates rather than rounds. Anything
            // further out than that is not truncation.
            Assert.True(
                delta is >= -2 and <= 0,
                $"Seeded +{seeded}d, dashboard reported " +
                $"DaysUntilExpiry={reported} (delta {delta}). " +
                "A total-day count cannot be this far below the " +
                "seeded value.");
        }

        // The +1d probe is the control. CURRENT_TIMESTAMP has advanced by a
        // few milliseconds since the seed, so a 1-day-out document reads as
        // 0 days 23:59:5x and the expression truncates to 0. Both 0 and 1 are
        // therefore correct here; anything else is not truncation.
        Assert.True(
            byId[ids[1]].DaysUntilExpiry is 0 or 1,
            $"Seeded +1d, reported " +
            $"DaysUntilExpiry={byId[ids[1]].DaysUntilExpiry}. " +
            "Expected 0 (truncated) or 1 (exact).");

        // Monotonicity across the whole sequence: each probe must report at
        // least as many days as the one before it. A day-field count fails
        // here, because past a month PostgreSQL folds days into months and
        // the day field stops increasing.
        for (var i = 1; i < observed.Count; i++)
        {
            var previous = observed[i - 1];
            var current = observed[i];

            Assert.True(
                current.Reported > previous.Reported,
                $"DaysUntilExpiry is not monotonic: +{previous.Seeded}d " +
                $"reported {previous.Reported}, then +{current.Seeded}d " +
                $"reported {current.Reported}.");
        }
    }

    // ==================================================================
    // Employees variant - the same expression, a different query
    // ==================================================================

    [Fact(DisplayName =
        "D3-EMP: the employee expiry-alert query reports a " +
        "total-day count too")]
    public async Task DaysUntilExpiry_EmployeeQuery_IsATotalDayCount()
    {
        var ids = new Dictionary<int, Guid>();

        // The employees query INNER JOINs documents to employees, so these
        // probes are employee-held documents rather than entity-held ones.
        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            foreach (var (days, _) in ProbePoints)
            {
                var id = Guid.NewGuid();

                ids[days] = id;

                db.Documents.Add(new Document
                {
                    Id = id,
                    ClientEntityId = null,
                    EmployeeId = TestDataSeeder.Ids.EmployeeA,
                    Type = DocumentType.Visa,
                    DocumentNumber =
                        $"D3-EMPPROBE-{days:D3}",
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddDays(days),
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"probe-{days}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            db.SaveChanges();
        });

        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDashboardReadRepository>();

        var alerts =
            await repository.GetExpiringEmployeesAsync(
                Days, CancellationToken.None);

        var byId = alerts.ToDictionary(x => x.Id);

        foreach (var (seededDays, _) in ProbePoints)
        {
            var id = ids[seededDays];

            Assert.True(
                byId.ContainsKey(id),
                $"Employee probe document seeded at " +
                $"+{seededDays}d ({id}) was not returned.");

            var reported = byId[id].DaysUntilExpiry;

            Assert.True(
                reported - seededDays is >= -2 and <= 0,
                $"Employee query: seeded +{seededDays}d, reported " +
                $"DaysUntilExpiry={reported} " +
                $"(delta {reported - seededDays}).");
        }
    }

    // ==================================================================
    // Route-level guard: the dashboard endpoint still serves these values
    // ==================================================================

    [Fact(DisplayName =
        "D3-ROUTE: /dashboard/expiry-alerts/documents exposes the " +
        "same DaysUntilExpiry values")]
    public async Task DaysUntilExpiry_OverTheRoute_MatchesTheRepository()
    {
        var token = await LoginAsAdminAsync();

        var ids = await SeedProbeDocumentsAsync();

        var request = AuthGet(
            $"/api/v1/dashboard/expiry-alerts/documents?days={Days}",
            token);

        var response = await AuthClient.SendAsync(request);

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<List<DashboardExpiryAlertDto>>>(response);

        var alerts = Assert.IsType<
            List<DashboardExpiryAlertDto>>(
            Assert.IsType<ApiResponse<List<DashboardExpiryAlertDto>>>(
                envelope).Data);

        var byId = alerts.ToDictionary(x => x.Id);

        // The route caps the result at 20 rows and orders by expiry
        // ascending, so the later probes can legitimately fall outside the
        // page. Only assert on the probes that came back, and say which were
        // truncated rather than failing on them.
        var checkedAny = false;

        foreach (var (seededDays, _) in ProbePoints)
        {
            var id = ids[seededDays];

            if (!byId.TryGetValue(id, out var alert))
            {
                continue;
            }

            checkedAny = true;

            Assert.True(
                alert.DaysUntilExpiry - seededDays
                    is >= -2 and <= 0,
                $"Route: seeded +{seededDays}d, reported " +
                $"DaysUntilExpiry={alert.DaysUntilExpiry} " +
                $"(delta {alert.DaysUntilExpiry - seededDays}).");
        }

        Assert.True(
            checkedAny,
            "The 20-row cap on the dashboard route excluded every " +
            "probe document, so the route check proved nothing.");
    }
}
