using System.Net;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// P7.3 / D4 SUITE. The Agent expiring endpoint's <c>days</c> parameter
/// contract.
///
/// THE DEFECT
///
/// <c>GetAgentExpiringDocumentsQueryValidator</c> accepted <c>Days</c> in
/// 1..365, but <c>GetAgentExpiringDocumentsQueryHandler</c> additionally
/// re-checked the same value against a hard-coded
/// <c>{ 30, 60, 90 }</c> whitelist and threw when it missed. The two layers
/// therefore contradicted each other: every value the validator permitted
/// outside those three constants was rejected further down the pipeline.
///
/// The pipeline behaviour (<c>ValidationBehaviour</c>) runs validators before
/// the handler, so the whitelist was the layer that decided the outcome. Any
/// value in 1..365 other than 30/60/90 produced
/// <c>400 BAD_REQUEST</c> with the body message
/// "Days must be one of: 30, 60, 90." - a message that directly disagreed
/// with the validator's own "Days must be between 1 and 365.". It was a
/// contract defect, not a crash: <c>GlobalExceptionMiddleware</c> maps
/// <c>ArgumentException</c> to 400, so nothing ever 500'd.
///
/// Provenance, from git history: at commit f93f47c the Agent query directory
/// contained only the Query and the Handler. The whitelist shipped with the
/// handler and no validator existed yet. The validator was added later, in the
/// same uncommitted wave that converted a family of endpoints from 500 to 400
/// (see the sibling DisplayNames in ValidationSemanticsTests, all of which read
/// "... is a 400 (was 500)"). The whitelist was simply never removed once the
/// validator landed. Admin documents/expiring and employees/expiring have
/// carried 1..365 plus a pass-through handler since the initial commit a5a1fea,
/// which makes that the original, canonical shape. The Agent handler was the
/// only handler in the whole Application layer containing an
/// <c>s_allowedDays</c> field.
///
/// The repository window is parameterised -
/// <c>SqlSnippets.ExpiryWindowCondition</c> emits
/// <c>expiry &lt;= CURRENT_TIMESTAMP + (@Days * INTERVAL '1 day')</c> - so
/// arbitrary positive widths are already supported and sargable against the
/// existing <c>documents.ExpiryDate</c> index. No SQL change was required.
///
/// WHY THESE EXPECTATIONS
///
/// VALID values are asserted to return 200 and, critically, to actually return
/// the seeded document. Asserting only the status code would let an empty
/// result set pass, which is precisely how a wrong window could hide. Each
/// valid case therefore correlates by document id against a probe planted
/// inside that window and linked to the calling agent's renewal task.
///
/// days=45 is the key discriminating case: it is inside the validator's range,
/// outside the deleted whitelist, and its probe sits at +40 so the 45-day
/// window must admit it. Against the old code this was a 400; after the fix it
/// is a 200 carrying that exact document.
///
/// INVALID values (0, -1, 366) must be rejected by the validator, and the
/// assertions require the body to contain <c>VALIDATION_ERROR</c> rather than
/// merely a 400 status. That distinction matters: the removed whitelist
/// produced 400 <c>BAD_REQUEST</c>, the validator produces 400
/// <c>VALIDATION_ERROR</c>. Requiring the code pins rejection to the intended
/// layer. The lower bound of 1 is deliberate and must not be relaxed - the
/// window is an interval multiplication, so a zero or negative width is
/// meaningless, and the validator is the guard that keeps it sane.
///
/// SCOPING
///
/// D4 widens the window, never the audience. The Agent scope predicate
/// (<c>SqlSnippets.DocumentScopeExists</c>) is an EXISTS over renewal_tasks
/// keyed on <c>AssignedStaffId = @AgentId</c> and carries no window parameter,
/// so no value of days can admit a document outside the agent's task-linked
/// portfolio. The scoping case plants two same-company, same-entity documents
/// at +300 days, one task-linked to the calling agent and one to another agent,
/// and asserts only the first is returned at days=365. This is what a
/// cross-agent leak would look like, so the window is pushed to its maximum
/// legal value to make the test as adversarial as the contract allows.
///
/// Note also that the Agent document list
/// (<c>GET /api/v1/agent/documents</c>) applies no expiry window at all, so
/// every in-scope document is already visible to the agent with its expiry
/// date. A 365-day expiring window therefore discloses nothing new.
///
/// CORRELATION DISCIPLINE
///
/// Every assertion correlates by document id. Nothing depends on row position
/// or a global row count, because the shared seed already plants documents
/// inside these same windows. Each test resets the database through
/// <c>IntegrationTestBase.InitializeAsync</c>, so no test observes another's
/// rows. Probe points are deliberately off exact boundaries (no probe sits at
/// +30, +60, +90 or +365) so a drifting CURRENT_TIMESTAMP cannot flip a
/// comparison mid-run; this is a parameter-contract suite, not a D1/D2 status
/// or window-boundary suite.
///
/// CLIENT / ADMIN BOUNDARY
///
/// Client documents/expiring remains restricted to { 30, 60, 90 } by its own
/// validator and remains pinned by ClientTenancy/ClientEmployeeDocumentApiTests
/// (X2 accepts 60 and 90; X3 rejects 120). Admin documents/expiring and
/// employees/expiring remain 1..365. This suite is Agent-only and asserts
/// nothing about the other two.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AgentExpiringDaysContractTests : IntegrationTestBase
{
    // Off-boundary probe points. None coincides with a tested window edge.
    private const int InsideDayOne = 1;
    private const int InsideThirty = 25;
    private const int InsideFortyFive = 40;
    private const int InsideSixty = 50;
    private const int InsideNinety = 80;
    private const int InsideNinetyOne = 85;
    private const int InsideThreeSixtyFive = 300;

    private const int PageSize = 100;

    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid AgentB = TestDataSeeder.Ids.AgentB;

    public AgentExpiringDaysContractTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private sealed record ProbeSet(
        Guid InsideOne,
        Guid InsideTwentyFive,
        Guid InsideForty,
        Guid InsideFifty,
        Guid InsideEighty,
        Guid InsideEightyFive,
        Guid InsideThreeHundred,
        Guid OtherAgentInsideThreeHundred);

    // ==================================================================
    // Seeding
    // ==================================================================

    /// <summary>
    /// Plants one document per tested window, all owned by the seeded Entity A
    /// so the calling agent's task-linked scope resolves without adding
    /// companies or entities. Every expiry date is anchored to a single
    /// captured <c>now</c>.
    ///
    /// The last probe is owned by the same Entity A and the same company, but
    /// its renewal task is assigned to Agent B. For Agent A the only possible
    /// reason to exclude it is portfolio scoping, never ownership or tenancy.
    /// </summary>
    private async Task<ProbeSet> SeedProbesAsync()
    {
        var insideOne = Guid.NewGuid();
        var insideTwentyFive = Guid.NewGuid();
        var insideForty = Guid.NewGuid();
        var insideFifty = Guid.NewGuid();
        var insideEighty = Guid.NewGuid();
        var insideEightyFive = Guid.NewGuid();
        var insideThreeHundred = Guid.NewGuid();
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
                    DocumentNumber = $"D4-{id:N}"[..20],
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddDays(days),
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"d4-probe-{days}.pdf",
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

            AddDocument(insideOne, InsideDayOne);
            AddDocument(insideTwentyFive, InsideThirty);
            AddDocument(insideForty, InsideFortyFive);
            AddDocument(insideFifty, InsideSixty);
            AddDocument(insideEighty, InsideNinety);
            AddDocument(insideEightyFive, InsideNinetyOne);
            AddDocument(insideThreeHundred, InsideThreeSixtyFive);
            AddDocument(otherAgent, InsideThreeSixtyFive);

            foreach (var id in new[]
                     {
                         insideOne, insideTwentyFive, insideForty,
                         insideFifty, insideEighty, insideEightyFive,
                         insideThreeHundred
                     })
            {
                AddTask(id, AgentA);
            }

            AddTask(otherAgent, AgentB);

            db.SaveChanges();
        });

        return new ProbeSet(
            insideOne, insideTwentyFive, insideForty, insideFifty,
            insideEighty, insideEightyFive, insideThreeHundred, otherAgent);
    }

    // ==================================================================
    // Call helpers
    // ==================================================================

    private Task<HttpResponseMessage> SendExpiringAsync(
        int days, bool includeExpired = false) =>
        ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/documents/expiring?days={days}&page=1" +
                $"&pageSize={PageSize}&includeExpired={includeExpired}",
                AgentA, role: "Agent"));

    private async Task<List<ExpiringDocumentListItemDto>> ItemsAsync(
        int days, bool includeExpired = false)
    {
        var response = await SendExpiringAsync(days, includeExpired);

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetExpiringDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<GetExpiringDocumentsResponseDto>(
            envelope!.Data).Items;
    }

    /// <summary>
    /// Asserts a rejection came from the validator pipeline stage rather than
    /// from a handler-thrown exception. The removed whitelist answered 400
    /// BAD_REQUEST; FluentValidation answers 400 VALIDATION_ERROR. Requiring
    /// the code is what pins rejection to the intended layer.
    /// </summary>
    private static async Task AssertValidationRejectionAsync(
        HttpResponseMessage response, int days)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("VALIDATION_ERROR", body);
        Assert.DoesNotContain(
            "BAD_REQUEST",
            body);
        Assert.DoesNotContain(
            "must be one of",
            body);
        Assert.DoesNotContain(
            $"days={days}",
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asserts a specific seeded document came back for a specific window.
    /// Correlates by id and reports the window and probe offset on failure, so
    /// a wrong-window regression is distinguishable from a scope regression.
    /// </summary>
    private static void AssertWindowAdmits(
        IEnumerable<ExpiringDocumentListItemDto> items,
        Guid expectedId,
        int days,
        int probeDays)
    {
        Assert.True(
            items.Any(item => item.Id == expectedId),
            $"days={days} did not admit the task-linked document seeded at " +
            $"+{probeDays}d (id {expectedId}). Returned ids: " +
            $"{string.Join(", ", items.Select(item => item.Id))}.");
    }

    private static void AssertWindowExcludes(
        IEnumerable<ExpiringDocumentListItemDto> items,
        Guid unexpectedId,
        int days,
        int probeDays)
    {
        Assert.True(
            !items.Any(item => item.Id == unexpectedId),
            $"days={days} admitted the document seeded at +{probeDays}d " +
            $"(id {unexpectedId}) although it is outside that window.");
    }

    // ==================================================================
    // D4-1  valid windows
    // ==================================================================

    [Theory(DisplayName =
        "D4-1: agent expiring documents accept days={0} (200 + document)")]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(91)]
    [InlineData(365)]
    public async Task ValidDays_ReturnOkWithSeededDocument(
        int days)
    {
        var probes = await SeedProbesAsync();

        var (expectedId, probeDays) = days switch
        {
            1 => (probes.InsideOne, InsideDayOne),
            30 => (probes.InsideTwentyFive, InsideThirty),
            45 => (probes.InsideForty, InsideFortyFive),
            60 => (probes.InsideFifty, InsideSixty),
            90 => (probes.InsideEighty, InsideNinety),
            91 => (probes.InsideEightyFive, InsideNinetyOne),
            _ => (probes.InsideThreeHundred, InsideThreeSixtyFive)
        };

        var response = await SendExpiringAsync(days);

        await EnsureOk(response, HttpStatusCode.OK);

        // The status code alone is not the contract: an empty 200 would pass
        // it. The seeded document proves the request reached the repository
        // and that the requested width was actually applied.
        var envelope = await ReadJsonAsync<
            ApiResponse<GetExpiringDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        var items = Assert.IsType<GetExpiringDocumentsResponseDto>(
            envelope!.Data).Items;

        AssertWindowAdmits(
            items, expectedId, days, probeDays);

        // The removed whitelist answered 400 BAD_REQUEST, so assert its
        // distinctive marker is absent even if the status were re-mapped.
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("BAD_REQUEST", body);
        Assert.DoesNotContain("VALIDATION_ERROR", body);
    }

    // ==================================================================
    // D4-2  the key discriminating case
    // ==================================================================

    [Fact(DisplayName =
        "D4-2: days=45 reaches the repository and returns the +40d document " +
        "(was 400 BAD_REQUEST from the handler whitelist)")]
    public async Task FortyFiveDays_ReturnsWindowedDocument()
    {
        var probes = await SeedProbesAsync();

        var items = await ItemsAsync(InsideFortyFive);

        // 45 is inside the validator's 1..365 range and outside the deleted
        // { 30, 60, 90 } whitelist. Against the old handler this request was
        // rejected with 400 before any repository call. The +40d probe sits
        // inside a 45-day window and 5 days outside a 30-day one, so its
        // presence also proves the requested width - not merely the absence
        // of an exception.
        AssertWindowAdmits(
            items, probes.InsideForty, InsideFortyFive, InsideFortyFive);

        // The +25d probe is inside every tested window; asserting it rules out
        // a response that ignored the parameter and returned a fixed set.
        AssertWindowAdmits(
            items, probes.InsideTwentyFive, InsideFortyFive, InsideThirty);
    }

    // ==================================================================
    // D4-3  wider than the standard windows
    // ==================================================================

    [Fact(DisplayName =
        "D4-3: days=365 admits a +300d in-scope document")]
    public async Task ThreeSixtyFiveDays_AdmitsFarFutureDocument()
    {
        var probes = await SeedProbesAsync();

        var items = await ItemsAsync(InsideThreeSixtyFive);

        // A 365-day window must reach a document 300 days out. The old code
        // refused days=365 outright, so this could not previously be observed
        // through the route at all.
        AssertWindowAdmits(
            items,
            probes.InsideThreeHundred,
            InsideThreeSixtyFive,
            InsideThreeSixtyFive);

        // A document 80 days out is inside a 365-day window too.
        AssertWindowAdmits(
            items,
            probes.InsideEighty,
            InsideThreeSixtyFive,
            InsideNinety);
    }

    // ==================================================================
    // D4-4  scope is not widened by a wider window
    // ==================================================================

    [Fact(DisplayName =
        "D4-4: days=365 does not leak another agent's same-company document")]
    public async Task ThreeSixtyFiveDays_DoesNotBypassAgentScope()
    {
        var probes = await SeedProbesAsync();

        var items = await ItemsAsync(InsideThreeSixtyFive);

        // Same company, same entity, same expiry - the only difference is
        // which agent owns the renewal task. Widening the window must not
        // change that.
        AssertWindowAdmits(
            items,
            probes.InsideThreeHundred,
            InsideThreeSixtyFive,
            InsideThreeSixtyFive);

        AssertWindowExcludes(
            items,
            probes.OtherAgentInsideThreeHundred,
            InsideThreeSixtyFive,
            InsideThreeSixtyFive);
    }

    // ==================================================================
    // D4-5  invalid widths still rejected by the validator
    // ==================================================================

    [Theory(DisplayName =
        "D4-5: agent expiring documents reject days={0} (400 VALIDATION_ERROR)")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task InvalidDays_ReturnValidation400(int days)
    {
        await SeedProbesAsync();

        var response = await SendExpiringAsync(days);

        await AssertValidationRejectionAsync(response, days);
    }

    // ==================================================================
    // D4-6  the standard windows are unchanged
    // ==================================================================

    [Fact(DisplayName =
        "D4-6: days=30 keeps its original 30-day window")]
    public async Task ThirtyDays_KeepsOriginalWindow()
    {
        var probes = await SeedProbesAsync();

        var items = await ItemsAsync(30);

        // +25d is inside a 30-day window.
        AssertWindowAdmits(
            items, probes.InsideTwentyFive, 30, InsideThirty);

        // +40d is outside it. This is the pre-existing Agent behaviour that
        // P7.1/D1 also relies on, and it must survive the whitelist removal.
        AssertWindowExcludes(
            items, probes.InsideForty, 30, InsideFortyFive);
    }
}
