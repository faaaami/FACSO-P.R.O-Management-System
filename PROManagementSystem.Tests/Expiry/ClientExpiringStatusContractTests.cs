using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// P8 CONTRACT SUITE. Client expiring-document status contract.
///
/// WHY THIS SUITE EXISTS
///
/// The P8 defect was entirely client-side: both Client views reconstructed
/// expiry state with <c>daysRemaining &lt;= 0</c> and rendered that as
/// "Overdue" with a danger tone. The backend was already correct and was NOT
/// changed by P8 - <c>DocumentExpiryReadRepository.GetExpiringByCompanyIdAsync</c>
/// has always selected <c>SqlSnippets.DaysRemaining</c> and
/// <c>SqlSnippets.ExpiryStatusLabelCase(..., 90)</c>.
///
/// The bug was possible only because the response contains BOTH a signed
/// number and a status label, and only one of them can be authoritative. This
/// suite pins the invariant the fixed UI now depends on:
///
///     a future document whose daysRemaining is 0 MUST be "ExpiringSoon",
///     and MUST NOT be reported as "Expired".
///
/// There is no clock abstraction, so CURRENT_TIMESTAMP advances during
/// execution. Probes are therefore never planted on an exact boundary.
///
/// THE ZERO-DAY PROBE (C-1, the discriminating case)
///
/// <c>DaysRemaining</c> is
/// FLOOR(EXTRACT(EPOCH FROM (expiry_date - CURRENT_TIMESTAMP)) / 86400). The
/// old <c>expiry_date::date - CURRENT_DATE::date</c> expression rounded UP, so
/// the two disagreed by one day exactly where it matters most.
///
/// A document planted at <c>now + 12 hours</c> is the whole reason the P8 bug
/// was user-visible: 0.5 days elapsed floors to <c>0</c>, yet the document has
/// not expired. Planting at 12 hours rather than at a few seconds deliberately
/// keeps the probe away from BOTH bucket edges - the reported value stays 0
/// for any query run within 12 hours of the seed, in either direction, so this
/// is a margin-based probe and not a boundary one. The same probe is the
/// cheapest possible proof that sign-of-daysRemaining is not expiry state.
///
/// IN-REACHABILITY OF EACH STATUS ON THE CLIENT ROUTE
///
/// The route's validator restricts <c>Days</c> to 30/60/90 and the window is
/// <c>expiry &lt;= CURRENT_TIMESTAMP + (@Days * INTERVAL '1 day')</c>, while the
/// status CASE uses a fixed 90-day threshold. So over HTTP:
///   - "Expired"    is reachable only with includeExpired=true (lower bound dropped)
///   - "ExpiringSoon" is reachable for any allowed window
///   - "Active"     is UNREACHABLE, because it needs
///                   expiry &gt; now + 90d while the widest legal window admits
///                   only expiry &lt;= now + 90d. The two bounds are exclusive.
///
/// C-4 therefore calls the repository directly at a 365-day width. That is a
/// test-only observation of a branch the HTTP surface cannot reach, and it
/// mirrors the WideWindowDays technique already used by
/// AgentExpiringStatusContractTests. No handler, validator or controller is
/// involved and no production behaviour is altered to make it observable.
///
/// CORRELATION DISCIPLINE
///
/// Every assertion correlates by document id. Nothing depends on row position
/// or a global row count, because the shared seed already plants documents
/// inside these same windows. Each test resets the database
/// (IntegrationTestBase.InitializeAsync), so no test observes another's rows.
///
/// NOT IN SCOPE
///
/// P5-C9 (Billing/PaymentOrderCancellationTests) is a separate backlog item and
/// is untouched here. This suite adds no client, no route and no seed data
/// outside the existing TestDataSeeder identities.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientExpiringStatusContractTests : IntegrationTestBase
{
    /// <summary>
    /// The window the Client UI actually requests. P8 must not change it, so the
    /// suite drives it to prove the fix under the real production window.
    /// </summary>
    private const int RouteDays = 30;

    /// <summary>
    /// Width used only for the repository-level "Active" observation, which the
    /// validator's 30/60/90 rule makes unreachable over HTTP. 365 is far from
    /// the +200d probe, so no boundary is involved.
    /// </summary>
    private const int WideWindowDays = 365;

    private const int PageSize = 100;

    /// <summary>
    /// Planted 12 hours out: inside the forward-looking window, but only half a
    /// day of exact elapsed time, which is the [0, 1) floor bucket.
    /// </summary>
    private static readonly TimeSpan NearExpiryOffset = TimeSpan.FromHours(12);

    private const int ExpiredDays = -3;
    private const int PlusOne = 1;
    private const int PlusTwentyNine = 29;

    /// <summary>Beyond the 90-day status threshold, observed at repository level only.</summary>
    private const int PlusTwoHundred = 200;

    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;

    public ClientExpiringStatusContractTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private sealed record ProbeSet(
        Guid NearExpiry,
        Guid Expired,
        Guid PlusOne,
        Guid PlusTwentyNine,
        Guid PlusTwoHundred,
        Guid OtherCompany);

    // ==================================================================
    // Seeding
    // ==================================================================

    /// <summary>
    /// Plants one document per probe, all client-entity scoped to the seeded
    /// Entity A so Company A's tenancy predicate resolves without extra
    /// companies or entities. The final probe belongs to Entity B (Company B)
    /// so the only reason it can be excluded is company scoping.
    /// </summary>
    private async Task<ProbeSet> SeedProbesAsync()
    {
        var nearExpiry = Guid.NewGuid();
        var expired = Guid.NewGuid();
        var plusOne = Guid.NewGuid();
        var plusTwentyNine = Guid.NewGuid();
        var plusTwoHundred = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            void AddDocument(
                Guid id,
                Guid entityId,
                DateTime expiryDate,
                string tag)
            {
                db.Documents.Add(new Document
                {
                    Id = id,
                    ClientEntityId = entityId,
                    EmployeeId = null,
                    Type = DocumentType.Visa,
                    DocumentNumber = $"P8-{id:N}"[..20],
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = expiryDate,
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"p8-probe-{tag}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            AddDocument(
                nearExpiry, TestDataSeeder.Ids.EntityA,
                now.Add(NearExpiryOffset), "near");
            AddDocument(
                expired, TestDataSeeder.Ids.EntityA,
                now.AddDays(ExpiredDays), "expired");
            AddDocument(
                plusOne, TestDataSeeder.Ids.EntityA,
                now.AddDays(PlusOne), "p1");
            AddDocument(
                plusTwentyNine, TestDataSeeder.Ids.EntityA,
                now.AddDays(PlusTwentyNine), "p29");
            AddDocument(
                plusTwoHundred, TestDataSeeder.Ids.EntityA,
                now.AddDays(PlusTwoHundred), "p200");
            AddDocument(
                otherCompany, TestDataSeeder.Ids.EntityB,
                now.AddDays(PlusTwentyNine), "other");

            db.SaveChanges();
        });

        return new ProbeSet(
            nearExpiry, expired, plusOne, plusTwentyNine,
            plusTwoHundred, otherCompany);
    }

    // ==================================================================
    // Call helpers
    // ==================================================================

    private async Task<List<ExpiringDocumentListItemDto>>
        GetClientExpiringAsync(int days, bool includeExpired)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/documents/expiring?days={days}&page=1" +
                $"&pageSize={PageSize}&includeExpired={includeExpired}",
                ClientA, role: "Client",
                clientCompanyId: CompanyA.ToString()));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetExpiringDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<GetExpiringDocumentsResponseDto>(
            envelope!.Data).Items;
    }

    /// <summary>
    /// Calls the repository the Client handler itself calls, at a width the
    /// HTTP validator would refuse. Test-only: no handler, validator or
    /// controller participates. This is the only way to observe "Active" for
    /// the Client, because the 30/60/90 rule makes that branch unreachable
    /// over HTTP.
    /// </summary>
    private async Task<List<ExpiringDocumentListItemDto>>
        GetClientExpiringAtRepositoryAsync(int days, bool includeExpired)
    {
        await using var scope =
            Factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IDocumentExpiryReadRepository>();

        var result = await repository.GetExpiringByCompanyIdAsync(
            CompanyA, days, 1, PageSize, includeExpired,
            CancellationToken.None);

        return result.Items;
    }

    private static ExpiringDocumentListItemDto RowById(
        IEnumerable<ExpiringDocumentListItemDto> items,
        Guid id,
        string tag)
    {
        var matches = items.Where(x => x.Id == id).ToList();

        Assert.True(
            matches.Count == 1,
            $"Expected exactly one expiring row for the {tag} probe " +
            $"({id}), found {matches.Count}.");

        return matches[0];
    }

    // ==================================================================
    // C-1: the zero-day future document (DISCRIMINATING)
    // ==================================================================

    [Fact(DisplayName =
        "P8-C1: a future document with daysRemaining 0 is 'ExpiringSoon', never 'Expired'")]
    public async Task FutureZeroDayDocument_IsExpiringSoonNotExpired()
    {
        var probes = await SeedProbesAsync();

        // includeExpired=false is what the Client UI sends; the fixed rows
        // must still come back.
        var items = await GetClientExpiringAsync(
            RouteDays, includeExpired: false);

        var row = RowById(items, probes.NearExpiry, "12-hour near-expiry");

        // THE core assertion. A document 12 hours in the future is not
        // expired, and the server says so.
        Assert.Equal("ExpiringSoon", row.Status);

        // And the number really is 0, which is the whole reason the old
        // daysRemaining <= 0 test mislabelled it. Exact equality is safe
        // because the probe sits half a day inside the [0, 1) floor bucket.
        Assert.Equal(
            0, row.DaysRemaining);

        // Guard the guard: the row's own expiry must be in the future, so a
        // future status cannot be satisfied by a clock that already passed the
        // expiry date. Tolerance covers the drift between the seed and query.
        Assert.NotNull(row.ExpiryDate);
        Assert.True(
            row.ExpiryDate!.Value >= DateTime.UtcNow.AddMinutes(-5),
            $"The {NearExpiryOffset.TotalHours:0}-hour probe reported an " +
            $"expiry of {row.ExpiryDate:o} that is already in the past, so " +
            "this case did not actually exercise a future document.");
    }

    // ==================================================================
    // C-2: ordinary future documents
    // ==================================================================

    [Theory(DisplayName =
        "P8-C2: a +{0}d document is 'ExpiringSoon' with floored daysRemaining")]
    [InlineData(PlusOne)]
    [InlineData(PlusTwentyNine)]
    public async Task InsideStatusWindow_IsExpiringSoon(
        int seededDays)
    {
        var probes = await SeedProbesAsync();

        var items = await GetClientExpiringAsync(
            RouteDays, includeExpired: false);

        var id = seededDays switch
        {
            PlusOne => probes.PlusOne,
            PlusTwentyNine => probes.PlusTwentyNine,
            _ => throw new
                ArgumentOutOfRangeException(nameof(seededDays))
        };

        var row = RowById(items, id, $"+{seededDays}d");

        Assert.Equal("ExpiringSoon", row.Status);

        // The canonical floor reports D-1 once the query runs after the seed;
        // D is tolerated only for a zero-drift clock. The old calendar-date
        // expression reported D here, so this tolerance necessarily spans both
        // and is a regression guard rather than a discriminator. The sign is
        // never negative for a future document.
        Assert.True(
            row.DaysRemaining == seededDays - 1 ||
            row.DaysRemaining == seededDays,
            $"Document seeded at +{seededDays}d reported " +
            $"DaysRemaining={row.DaysRemaining}, expected " +
            $"{seededDays - 1} (or {seededDays} under zero clock drift).");

        Assert.True(
            row.DaysRemaining >= 0,
            $"A future document reported the negative " +
            $"DaysRemaining={row.DaysRemaining}.");
    }

    // ==================================================================
    // C-3: expired vocabulary, via the existing includeExpired parameter
    // ==================================================================

    [Fact(DisplayName =
        "P8-C3: an expired document is reported with status 'Expired'")]
    public async Task ExpiredDocument_ReportsExpiredStatus()
    {
        var probes = await SeedProbesAsync();

        // includeExpired=true drops only the LOWER bound of the window. It is
        // an existing production parameter, used here unchanged; P8 does not
        // alter it to make the assertion reachable.
        var items = await GetClientExpiringAsync(
            RouteDays, includeExpired: true);

        var row = RowById(items, probes.Expired, $"{ExpiredDays}d");

        Assert.Equal("Expired", row.Status);

        // Signed, so an expired document reports a negative figure rather
        // than a clamped 0.
        Assert.True(
            row.DaysRemaining < 0,
            $"Document seeded at {ExpiredDays}d reported " +
            $"DaysRemaining={row.DaysRemaining}. The canonical expression " +
            "is signed, so an expired document must report a negative value.");
    }

    [Fact(DisplayName =
        "P8-C4: the Client 30-day list excludes expired documents (forward-looking)")]
    public async Task ForwardLookingWindow_ExcludesExpiredDocuments()
    {
        var probes = await SeedProbesAsync();

        // This is the exact request the Client UI sends. It must stay
        // forward-looking, which is also why the UI's "Expired" branch is
        // defensive: a row with status "Expired" can never reach this list.
        var items = await GetClientExpiringAsync(
            RouteDays, includeExpired: false);

        Assert.False(
            items.Any(x => x.Id == probes.Expired),
            "The forward-looking 30-day window returned the expired probe. " +
            "P8 must not change includeExpired behaviour.");

        // Honest-green guard: the near-expiry probe IS in this result set, so
        // the exclusion above is not vacuous.
        Assert.True(
            items.Any(x => x.Id == probes.NearExpiry),
            "The 12-hour probe was absent, so the expired-document exclusion " +
            "check above had nothing to verify.");
    }

    // ==================================================================
    // C-5: "Active" - repository level only (unreachable over HTTP)
    // ==================================================================

    [Fact(DisplayName =
        "P8-C5: beyond the 90-day threshold a document is 'Active' (repository level)")]
    public async Task BeyondStatusWindow_IsActive_AtRepositoryLevel()
    {
        var probes = await SeedProbesAsync();

        // Reachable only here. The validator caps Days at 90 and the status
        // CASE needs expiry > now + 90d, so no legal HTTP window can show
        // this branch. Called at 365 days, far from the +200d probe.
        var items = await GetClientExpiringAtRepositoryAsync(
            WideWindowDays, includeExpired: false);

        var row = RowById(items, probes.PlusTwoHundred, $"+{PlusTwoHundred}d");

        Assert.Equal("Active", row.Status);

        Assert.True(
            row.DaysRemaining is PlusTwoHundred or (PlusTwoHundred - 1),
            $"Document seeded at +{PlusTwoHundred}d reported " +
            $"DaysRemaining={row.DaysRemaining}, expected " +
            $"{PlusTwoHundred - 1} (or {PlusTwoHundred} under zero drift).");
    }

    // ==================================================================
    // C-6: vocabulary guard (DISCRIMINATING)
    // ==================================================================

    [Fact(DisplayName =
        "P8-C6: no Client expiring row is labelled 'Overdue'")]
    public async Task NoRowUsesOverdueVocabulary()
    {
        await SeedProbesAsync();

        var items = await GetClientExpiringAsync(
            RouteDays, includeExpired: true);

        var offenders = items
            .Where(x => x.Status == "Overdue")
            .Select(x => x.Id)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"The Client expiring response uses the stored-enum " +
            $"'Overdue' vocabulary on {offenders.Count} row(s): " +
            $"{string.Join(", ", offenders)}. The canonical label for an " +
            "expired document on this endpoint is 'Expired'.");

        Assert.True(
            items.Any(x => x.Status == "Expired"),
            "No row carried status 'Expired', so the 'Overdue' check above " +
            "had nothing to verify.");
    }

    // ==================================================================
    // C-7: tenancy unchanged
    // ==================================================================

    [Fact(DisplayName =
        "P8-C8: a document from another company stays out of the Client list")]
    public async Task OtherCompanyDocument_IsExcluded()
    {
        var probes = await SeedProbesAsync();

        var items = await GetClientExpiringAsync(
            RouteDays, includeExpired: true);

        Assert.False(
            items.Any(x => x.Id == probes.OtherCompany),
            "A document scoped to Company B leaked into Company A's Client " +
            "expiring list. The tenancy predicate is enforced in SQL and " +
            "P8 must not change it.");

        // Same window, same status, so exclusion is attributable to the
        // company predicate alone and not to the window.
        Assert.True(
            items.Any(x => x.Id == probes.PlusTwentyNine),
            "The +29d Company A probe was absent, so the cross-company " +
            "exclusion check above had nothing to verify.");
    }
}
