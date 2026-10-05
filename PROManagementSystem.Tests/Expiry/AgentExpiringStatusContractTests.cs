using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// P7.2 / D2 REGRESSION SUITE. Agent expiring-document response contract.
///
/// THE DEFECT
///
/// <c>RenewalTaskReadRepository.GetAgentExpiringDocumentsAsync</c> built its
/// response fields with two private inline expressions instead of the shared
/// canonical snippets, so the Agent expiring endpoint disagreed with the
/// Admin and Client expiring endpoints - which already use the canonical pair:
///
///   old Agent DaysRemaining  GREATEST(0, expiry_date::date - CURRENT_DATE::date)
///   old Agent Status         CASE ... 'Overdue' ...
///
///   canonical DaysRemaining  SqlSnippets.DaysRemaining            (signed, floored)
///   canonical Status         SqlSnippets.ExpiryStatusLabelCase    ('Expired')
///
/// The divergence had two visible effects. Expired documents reported
/// daysRemaining = 0, losing the "how long ago" signal entirely, and they were
/// labelled 'Overdue' where every other expiring endpoint says 'Expired'.
/// There was also a one-day skew on every future document, because the old
/// expression compared calendar dates (rounding up) while the canonical
/// expression floors the exact elapsed 24-hour periods.
///
/// WHY THESE EXPECTATIONS
///
/// The canonical expression is
/// FLOOR(EXTRACT(EPOCH FROM (expiry_date - CURRENT_TIMESTAMP)) / 86400), so for
/// a probe seeded at now + D the reported value is D - 1 whenever the query runs
/// at all after the seed: the sub-second drift puts the true elapsed time just
/// below D whole days and the floor takes it down. P7.0 measured this on the
/// dashboard and recorded +1 => 0, +31 => 30, +60 => 59, +89 => 88, which this
/// suite reuses rather than re-deriving. Every daysRemaining assertion therefore
/// accepts {D-1, D}; the D-1 value is the real one and D is tolerated only for
/// the degenerate case of a zero-drift clock.
///
/// There is no clock abstraction here, so CURRENT_TIMESTAMP advances during
/// execution. The probe points stop at +89 and +91 and never assert an exact
/// +90, which would be ambiguous against an inclusive &lt;= boundary that
/// advances under the test. That follows the convention in
/// DocumentExpiryWindowTests.
///
/// The two DISCRIMINATING assertions are the expired-document sign and the
/// 'Overdue' vocabulary check. Both fail unambiguously against the old
/// implementation, which reported exactly 0 and exactly 'Overdue'. The
/// boundary-value assertions are tolerance guards rather than discriminators,
/// because the old and new expressions differ by one day there and the
/// tolerance necessarily spans both.
///
/// CORRELATION DISCIPLINE
///
/// Every assertion correlates by document id. Nothing depends on row position
/// or a global row count, because the shared seed already plants documents
/// inside these same windows. Each test also resets the database
/// (IntegrationTestBase.InitializeAsync), so no test observes another's rows.
///
/// THE WIDE-WINDOW PROBE
///
/// The +91 probe is observed by calling the repository directly with a window
/// of 120 days. This indirection was introduced during P7.2 because the Agent
/// handler still carried a hard-coded { 30, 60, 90 } whitelist, which made a
/// 120-day window unreachable through the HTTP route. P7.3 (D4) removed that
/// contradictory whitelist, so the Agent route now accepts any Days value in
/// the validator's 1..365 range and 120 is reachable over HTTP as well.
/// The repository-level call is kept deliberately: this suite is a D1/D2 status
/// contract suite, and its +91 -> Active observation is already proven and
/// verified, so the mechanism is not being re-litigated here. The window
/// constant has been renamed from RepositoryOnlyDays to WideWindowDays because
/// "RepositoryOnly" is no longer accurate.
///
/// D4's own parameter-contract coverage lives separately in
/// Validation/AgentExpiringDaysContractTests.cs.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AgentExpiringStatusContractTests : IntegrationTestBase
{
    /// <summary>
    /// The standard Agent window this suite drives over HTTP. It is no longer
    /// the largest value the route accepts - since P7.3 (D4) the route takes
    /// any 1..365 - but it is kept at 90 so this D1/D2 suite continues to
    /// exercise the canonical status threshold. With includeExpired the
    /// predicate is `expiry &lt;= now + 90 days`, which admits every probe
    /// except the +91 one.
    /// </summary>
    private const int RouteDays = 90;

    /// <summary>
    /// Wide window used for the +91 observation. Now a valid HTTP value too,
    /// since P7.3 (D4) aligned the Agent route with the validator's 1..365
    /// range; the repository-level call is retained as explained above.
    /// </summary>
    private const int WideWindowDays = 120;

    private const int PageSize = 100;

    private const int ExpiredDays = -1;

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid AgentB = TestDataSeeder.Ids.AgentB;

    public AgentExpiringStatusContractTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private sealed record ProbeSet(
        Guid Expired,
        Guid PlusOne,
        Guid PlusTwentyNine,
        Guid PlusThirtyOne,
        Guid PlusSixty,
        Guid PlusEightyNine,
        Guid PlusNinetyOne,
        Guid OtherAgentPlusSixty);

    // ==================================================================
    // Seeding
    // ==================================================================

    /// <summary>
    /// Plants one document per boundary probe, all owned by the seeded Entity A
    /// so the agent's task-linked scope resolves without planting additional
    /// companies or entities. Every expiry date is anchored to a single
    /// captured <c>now</c>.
    ///
    /// The final probe is owned by the same Entity A but its renewal task is
    /// assigned to Agent B, so for Agent A the only possible reason to exclude
    /// it is portfolio scoping.
    /// </summary>
    private async Task<ProbeSet> SeedProbesAsync()
    {
        var expired = Guid.NewGuid();
        var plusOne = Guid.NewGuid();
        var plusTwentyNine = Guid.NewGuid();
        var plusThirtyOne = Guid.NewGuid();
        var plusSixty = Guid.NewGuid();
        var plusEightyNine = Guid.NewGuid();
        var plusNinetyOne = Guid.NewGuid();
        var otherAgent = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            void AddDocument(Guid id, int days)
            {
                db.Documents.Add(new Document
                {
                    Id = id,
                    ClientEntityId = TestDataSeeder.Ids.EntityA,
                    EmployeeId = null,
                    Type = DocumentType.Visa,
                    DocumentNumber = $"D2-{id:N}"[..20],
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddDays(days),
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"d2-probe-{days}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            void AddTask(Guid documentId, Guid? agentId)
            {
                db.RenewalTasks.Add(new RenewalTask
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    ClientCompanyId = TestDataSeeder.Ids.CompanyA,
                    AssignedStaffId = agentId,
                    Status = RenewalTaskStatus.Submitted,
                    ServiceFeeAmount = 150m,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            AddDocument(expired, ExpiredDays);
            AddDocument(plusOne, 1);
            AddDocument(plusTwentyNine, 29);
            AddDocument(plusThirtyOne, 31);
            AddDocument(plusSixty, 60);
            AddDocument(plusEightyNine, 89);
            AddDocument(plusNinetyOne, 91);
            AddDocument(otherAgent, 60);

            foreach (var id in new[]
                     {
                         expired, plusOne, plusTwentyNine, plusThirtyOne,
                         plusSixty, plusEightyNine, plusNinetyOne
                     })
            {
                AddTask(id, AgentA);
            }

            AddTask(otherAgent, AgentB);

            db.SaveChanges();
        });

        return new ProbeSet(
            expired, plusOne, plusTwentyNine, plusThirtyOne,
            plusSixty, plusEightyNine, plusNinetyOne, otherAgent);
    }

    // ==================================================================
    // Call helpers
    // ==================================================================

    private async Task<List<ExpiringDocumentListItemDto>>
        GetAgentExpiringAsync(int days, bool includeExpired)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/documents/expiring?days={days}&page=1" +
                $"&pageSize={PageSize}&includeExpired={includeExpired}",
                AgentA, role: "Agent"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetExpiringDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<GetExpiringDocumentsResponseDto>(
            envelope!.Data).Items;
    }

    /// <summary>
    /// Calls the repository the handler itself calls, with a window the HTTP
    /// route would refuse. Test-only: no handler or validator is involved.
    /// </summary>
    private async Task<List<ExpiringDocumentListItemDto>>
        GetAgentExpiringAtRepositoryAsync(int days, bool includeExpired)
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IRenewalTaskReadRepository>();

        var result = await repository.GetAgentExpiringDocumentsAsync(
            AgentA, days, 1, PageSize, includeExpired,
            CancellationToken.None);

        return result.Items;
    }

    private static ExpiringDocumentListItemDto RowById(
        IEnumerable<ExpiringDocumentListItemDto> items,
        Guid id,
        int seededDays)
    {
        var matches = items.Where(x => x.Id == id).ToList();

        Assert.True(
            matches.Count == 1,
            $"Expected exactly one expiring row for the +{seededDays}d " +
            $"probe ({id}), found {matches.Count}.");

        return matches[0];
    }

    // ==================================================================
    // Expired document - status vocabulary
    // ==================================================================

    [Fact(DisplayName =
        "D2-1: an expired document is reported with status 'Expired'")]
    public async Task ExpiredDocument_ReportsExpiredStatus()
    {
        var probes = await SeedProbesAsync();

        var items = await GetAgentExpiringAsync(
            RouteDays, includeExpired: true);

        var row = RowById(items, probes.Expired, ExpiredDays);

        Assert.Equal("Expired", row.Status);
    }

    // ==================================================================
    // Expired document - signed daysRemaining (DISCRIMINATING)
    // ==================================================================

    [Fact(DisplayName =
        "D2-2: an expired document reports a NEGATIVE daysRemaining")]
    public async Task ExpiredDocument_ReportsNegativeDaysRemaining()
    {
        var probes = await SeedProbesAsync();

        var items = await GetAgentExpiringAsync(
            RouteDays, includeExpired: true);

        var row = RowById(items, probes.Expired, ExpiredDays);

        // The old implementation clamped this to 0 with GREATEST(0, ...), so
        // the sign alone is decisive: 0 cannot satisfy this.
        Assert.True(
            row.DaysRemaining < 0,
            $"Document seeded at {ExpiredDays}d reported " +
            $"DaysRemaining={row.DaysRemaining}. The canonical expression is " +
            "signed, so an expired document must report a negative value, not " +
            "a clamped 0.");

        // And it must be a plausible "days since expiry" figure rather than an
        // arbitrary negative: one day ago, floored, so -2 with -1 tolerated.
        Assert.True(
            row.DaysRemaining is -2 or -1,
            $"Document seeded at {ExpiredDays}d reported " +
            $"DaysRemaining={row.DaysRemaining}, expected -2 (or -1 under " +
            "zero clock drift).");
    }

    // ==================================================================
    // Boundary probes inside the 90-day status window
    // ==================================================================

    [Theory(DisplayName = "D2-3: a +{0}d document is ExpiringSoon with floored daysRemaining")]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(60)]
    [InlineData(89)]
    public async Task InsideStatusWindow_IsExpiringSoon(
        int seededDays)
    {
        var probes = await SeedProbesAsync();

        var items = await GetAgentExpiringAsync(
            RouteDays, includeExpired: true);

        var id = seededDays switch
        {
            1 => probes.PlusOne,
            29 => probes.PlusTwentyNine,
            31 => probes.PlusThirtyOne,
            60 => probes.PlusSixty,
            89 => probes.PlusEightyNine,
            _ => throw new ArgumentOutOfRangeException(nameof(seededDays))
        };

        var row = RowById(items, id, seededDays);

        Assert.Equal("ExpiringSoon", row.Status);

        // The canonical floor reports D-1 once the query runs after the seed;
        // D is tolerated only for a zero-drift clock. Written as equality
        // rather than a pattern because seededDays is a runtime argument.
        Assert.True(
            row.DaysRemaining == seededDays - 1 ||
            row.DaysRemaining == seededDays,
            $"Document seeded at +{seededDays}d reported " +
            $"DaysRemaining={row.DaysRemaining}, expected " +
            $"{seededDays - 1} (or {seededDays} under zero clock drift).");
    }

    // ==================================================================
    // Outside the status window - repository-level (D4 untouched)
    // ==================================================================

    [Fact(DisplayName =
        "D2-4: a +91 day document is 'Active' with signed daysRemaining")]
    public async Task OutsideStatusWindow_IsActive()
    {
        var probes = await SeedProbesAsync();

        // The repository is called directly to observe the 'Active' branch at a
        // 120-day window. Since P7.3 (D4) that width is also a valid HTTP value,
        // but the repository call is retained as the already-verified
        // mechanism for this D2 status assertion.
        var items = await GetAgentExpiringAtRepositoryAsync(
            WideWindowDays, includeExpired: false);

        var row = RowById(items, probes.PlusNinetyOne, 91);

        Assert.Equal("Active", row.Status);

        Assert.True(
            row.DaysRemaining is 90 or 91,
            $"Document seeded at +91d reported " +
            $"DaysRemaining={row.DaysRemaining}, expected 90 (or 91 under " +
            "zero clock drift).");
    }

    // ==================================================================
    // Vocabulary is 'Expired' everywhere (DISCRIMINATING)
    // ==================================================================

    [Fact(DisplayName =
        "D2-5: no Agent expiring row is labelled 'Overdue'")]
    public async Task NoRowUsesOverdueVocabulary()
    {
        await SeedProbesAsync();

        var items = await GetAgentExpiringAsync(
            RouteDays, includeExpired: true);

        var offenders = items
            .Where(x => x.Status == "Overdue")
            .Select(x => x.Id)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"The Agent expiring response still uses the divergent " +
            $"'Overdue' vocabulary on {offenders.Count} row(s): " +
            $"{string.Join(", ", offenders)}. The canonical label is " +
            "'Expired'.");

        // The expired probe is present in this result set, so the check above
        // had something to catch. Asserting that here keeps the test honest
        // rather than vacuously green on an empty list.
        Assert.True(
            items.Any(x => x.Status == "Expired"),
            "No row carried status 'Expired', so the 'Overdue' check above " +
            "had nothing to verify.");
    }

    // ==================================================================
    // Scoping is unchanged
    // ==================================================================

    [Fact(DisplayName =
        "D2-SCOPE: the Agent expiring list stays task-linked")]
    public async Task AgentExpiringList_RemainsTaskLinked()
    {
        var probes = await SeedProbesAsync();

        var items = await GetAgentExpiringAsync(
            RouteDays, includeExpired: true);

        // This probe is owned by the client's own entity and sits at +60d, so
        // it is inside both the window and any tenant check. Its renewal task
        // is assigned to another agent, so SqlSnippets.DocumentScopeExists is
        // the only thing that can exclude it. D2 must not have touched that.
        Assert.False(
            items.Any(x => x.Id == probes.OtherAgentPlusSixty),
            $"A +60d document assigned to another agent " +
            $"({probes.OtherAgentPlusSixty}) leaked into Agent A's expiring " +
            "results. Portfolio scoping must be unchanged.");

        // And the agent's own probes are still visible, so the exclusion above
        // is scoping and not an empty result set.
        Assert.True(
            items.Any(x => x.Id == probes.PlusSixty),
            "The agent's own +60d probe was missing, so the scoping check " +
            "above proved nothing.");
    }

    // ==================================================================
    // The user-facing days window is unchanged
    // ==================================================================

    [Fact(DisplayName =
        "D2-DAYS: a days=30 request keeps its 30-day window")]
    public async Task Days30_WindowIsUnchanged()
    {
        var probes = await SeedProbesAsync();

        var items = await GetAgentExpiringAsync(
            30, includeExpired: false);

        // D2 changes response semantics only. The caller-supplied days window
        // must still be 30, so +29 is in and +60 is out, even though +60 now
        // reports the canonical ExpiringSoon status.
        Assert.True(
            items.Any(x => x.Id == probes.PlusTwentyNine),
            "A +29d document is inside the 30-day window and must be " +
            "returned.");

        Assert.False(
            items.Any(x => x.Id == probes.PlusSixty),
            "A +60d document is outside the 30-day window and must not be " +
            "returned. If this fails, the response change has leaked into " +
            "the user-facing window.");
    }

    // ==================================================================
    // D1's status filter is unaffected
    // ==================================================================

    [Fact(DisplayName =
        "D2-D1: the Agent document-list status filter is unaffected")]
    public async Task DocumentListStatusFilter_RemainsIntact()
    {
        var probes = await SeedProbesAsync();

        // The document LIST is a different query (GetAgentDocumentsAsync) that
        // projects the numeric status, not this endpoint's string label. D2
        // must not have disturbed D1's 90-day filter work there.
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/documents?status=ExpiringSoon&page=1" +
                $"&pageSize={PageSize}",
                AgentA, role: "Agent"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetAgentDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        var items = Assert.IsType<GetAgentDocumentsResponseDto>(
            envelope!.Data).Items;

        Assert.True(
            items.Any(x => x.Id == probes.PlusSixty),
            $"A +60d document is projected ExpiringSoon by D1's canonical " +
            $"window and must be returned by status=ExpiringSoon " +
            $"({probes.PlusSixty}).");

        Assert.False(
            items.Any(x => x.Id == probes.PlusNinetyOne),
            "A +91d document is outside the canonical 90-day status window " +
            "and must not be returned by status=ExpiringSoon.");
    }
}
