using System.Net;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;
using PROManagementSystem.Application.Client.Queries.GetMyDocuments;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Expiry;

/// <summary>
/// P7.1 / D1 REGRESSION SUITE.
///
/// THE DEFECT
///
/// <c>SqlSnippets.DocumentStatusNumericCase</c> projects a document status on
/// a 90-day window, but two repository status FILTERS used a 30-day window:
///
///   DocumentReadRepository.GetByCompanyIdAsync      (client documents list)
///   RenewalTaskReadRepository.GetAgentDocumentsAsync (agent documents list)
///
/// A document around +60 days was therefore projected as ExpiringSoon and then
/// EXCLUDED by the very filter that claims to select ExpiringSoon. Both filters
/// also drive the complementary Active case, so Active and ExpiringSoon were
/// inconsistent with the projection in both directions.
///
/// This is not a latent inconsistency. Two live UI pages send the filter:
/// the client Documents page and the agent Documents page both render a Status
/// dropdown built from DOCUMENT_STATUS, which contains ExpiringSoon. Widening
/// 30 to 90 is therefore a deliberate, user-visible change of those lists.
///
/// WHAT IS ASSERTED HERE, AND WHAT IS NOT
///
/// D1 changes the STATUS FILTER semantics only. The status projection was
/// already 90 days and is unchanged. The user-facing expiry-list `days`
/// window is a separate concept and is deliberately still 30 days by default;
/// test Client_ExpiringList_WithDays30_ExcludesSixtyDayDocument pins that so a
/// future reader cannot conclude from the 30-to-90 change here that the
/// user-facing default was meant to move too. 60 days likewise remains a
/// selectable user-facing filter, not a business status threshold, so no test
/// here treats 60 as special beyond being the probe that distinguishes the old
/// 30-day filter from the canonical 90-day window.
///
/// PROBE DISCIPLINE
///
/// Probes are +60d and +91d. An exact +90 is never asserted: there is no clock
/// abstraction here, so CURRENT_TIMESTAMP advances while the test runs, and the
/// ExpiringSoon predicate is inclusive (<= now + 90 days), which would make an
/// exact-edge probe flaky for reasons unrelated to the behaviour under test.
/// +91 is unambiguously outside the window under any sub-second drift, and
/// +60 is unambiguously inside the 90-day window and unambiguously outside the
/// old 30-day filter. This mirrors the convention in DocumentExpiryWindowTests.
///
/// CORRELATION DISCIPLINE
///
/// Every assertion correlates by document id. Nothing depends on row position
/// or on a global row count, because the shared seed already plants documents
/// that fall inside these same windows. Each test also resets the database
/// (IntegrationTestBase.InitializeAsync), so no test observes another's rows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentStatusFilterWindowTests : IntegrationTestBase
{
    /// <summary>
    /// Inside the canonical 90-day status window, outside the old 30-day
    /// filter. This is the probe that the defect hid.
    /// </summary>
    private const int InsideDays = 60;

    /// <summary>
    /// Outside the canonical 90-day status window. One day past the edge so the
    /// inclusive <= comparison and clock drift cannot make it ambiguous.
    /// </summary>
    private const int OutsideDays = 91;

    /// <summary>
    /// Expired probe for the Overdue case, which is window-independent and must
    /// be unaffected by D1.
    /// </summary>
    private const int ExpiredDays = -1;

    /// <summary>
    /// The lists are paged, so every call asks for a full page and the
    /// assertions then correlate by id. 100 is the validator maximum.
    /// </summary>
    private const int PageSize = 100;

    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid AgentB = TestDataSeeder.Ids.AgentB;

    private static readonly string CompanyA =
        TestDataSeeder.Ids.CompanyA.ToString();

    public DocumentStatusFilterWindowTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// Ids of the documents planted by <see cref="SeedProbeDocumentsAsync"/>.
    /// </summary>
    private sealed record ProbeSet(
        Guid Inside,
        Guid Outside,
        Guid Expired,
        Guid OtherCompanyInside,
        Guid OtherAgentInside);

    // ==================================================================
    // Seeding
    // ==================================================================

    /// <summary>
    /// Plants five documents and the renewal tasks that define Agent A's
    /// portfolio.
    ///
    /// The two scoping probes are deliberately built to isolate ONE scoping
    /// rule each, so that a failure names the rule that broke rather than a
    /// combination:
    ///
    ///  - OtherCompanyInside is owned by Entity B but assigned to Agent A. An
    ///    agent portfolio legitimately spans companies, so it IS in Agent A's
    ///    scope and is asserted only for CLIENT isolation. Nothing else about it
    ///    can explain an exclusion.
    ///  - OtherAgentInside is owned by Entity A (the client's own company) but
    ///    its task is assigned to Agent B. For Agent A the only possible reason
    ///    to exclude it is portfolio scoping, which is exactly what is asserted.
    ///
    /// All expiry dates are anchored to a single captured <c>now</c>.
    /// </summary>
    private async Task<ProbeSet> SeedProbeDocumentsAsync()
    {
        var inside = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var expired = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var otherAgent = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            void AddDocument(Guid id, Guid entityId, int days)
            {
                db.Documents.Add(new Document
                {
                    Id = id,
                    ClientEntityId = entityId,
                    EmployeeId = null,
                    Type = DocumentType.Visa,
                    DocumentNumber = $"D1-{id:N}"[..20],
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddDays(days),
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"d1-probe-{days}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            void AddTask(
                Guid documentId,
                Guid companyId,
                Guid? agentId)
            {
                db.RenewalTasks.Add(new RenewalTask
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    ClientCompanyId = companyId,
                    AssignedStaffId = agentId,
                    Status = RenewalTaskStatus.Submitted,
                    ServiceFeeAmount = 150m,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                });
            }

            AddDocument(inside, TestDataSeeder.Ids.EntityA, InsideDays);
            AddDocument(outside, TestDataSeeder.Ids.EntityA, OutsideDays);
            AddDocument(expired, TestDataSeeder.Ids.EntityA, ExpiredDays);
            AddDocument(otherCompany, TestDataSeeder.Ids.EntityB, InsideDays);
            AddDocument(otherAgent, TestDataSeeder.Ids.EntityA, InsideDays);

            AddTask(inside, TestDataSeeder.Ids.CompanyA, AgentA);
            AddTask(outside, TestDataSeeder.Ids.CompanyA, AgentA);
            AddTask(expired, TestDataSeeder.Ids.CompanyA, AgentA);
            AddTask(otherCompany, TestDataSeeder.Ids.CompanyB, AgentA);
            AddTask(otherAgent, TestDataSeeder.Ids.CompanyA, AgentB);

            db.SaveChanges();
        });

        return new ProbeSet(
            inside, outside, expired, otherCompany, otherAgent);
    }

    // ==================================================================
    // Call helpers
    // ==================================================================

    private async Task<List<ClientDocumentListItemDto>>
        GetClientDocumentsAsync(DocumentStatus? status)
    {
        var url = $"/api/v1/client/documents?page=1&pageSize={PageSize}";

        if (status.HasValue)
        {
            url += $"&status={status.Value}";
        }

        var response = await ScopeClient.SendAsync(
            ScopeGet(url, ClientA, role: "Client",
                clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetClientDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<GetClientDocumentsResponseDto>(
            envelope!.Data).Items;
    }

    private async Task<List<AgentDocumentListItemDto>>
        GetAgentDocumentsAsync(DocumentStatus? status)
    {
        var url = $"/api/v1/agent/documents?page=1&pageSize={PageSize}";

        if (status.HasValue)
        {
            url += $"&status={status.Value}";
        }

        var response = await ScopeClient.SendAsync(
            ScopeGet(url, AgentA, role: "Agent"));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetAgentDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        return Assert.IsType<GetAgentDocumentsResponseDto>(
            envelope!.Data).Items;
    }

    /// <summary>
    /// Selects exactly one row by document id and reports the row count when it
    /// is anything other than one, so a "not returned" failure and a "returned
    /// twice" failure are distinguishable.
    /// </summary>
    private static T RowById<T>(
        IEnumerable<T> rows,
        Guid id,
        Func<T, Guid> idOf,
        string label)
    {
        var matches = rows.Where(x => idOf(x) == id).ToList();

        Assert.True(
            matches.Count == 1,
            $"Expected exactly one {label} row for document {id}, " +
            $"found {matches.Count}.");

        return matches[0];
    }

    // ==================================================================
    // A - the client ExpiringSoon filter returns the +60d document
    // ==================================================================

    [Fact(DisplayName =
        "D1-A: client status=ExpiringSoon returns a +60 day document")]
    public async Task Client_ExpiringSoonFilter_ReturnsSixtyDayDocument()
    {
        var probes = await SeedProbeDocumentsAsync();

        var filtered =
            await GetClientDocumentsAsync(DocumentStatus.ExpiringSoon);

        Assert.True(
            filtered.Any(x => x.Id == probes.Inside),
            $"Document seeded at +{InsideDays}d ({probes.Inside}) was " +
            "projected as ExpiringSoon but excluded by " +
            "status=ExpiringSoon. This is the D1 defect.");
    }

    // ==================================================================
    // B - that same document is projected ExpiringSoon
    // ==================================================================

    [Fact(DisplayName =
        "D1-B: the +60 day document is projected ExpiringSoon")]
    public async Task Client_ExpiringSoonProjection_LabelsSixtyDayDocument()
    {
        var probes = await SeedProbeDocumentsAsync();

        // Read with no status filter so the projection is observed
        // independently of the filter that is under test.
        var all = await GetClientDocumentsAsync(null);

        var row = RowById(all, probes.Inside, x => x.Id, "unfiltered");

        Assert.Equal(DocumentStatus.ExpiringSoon, row.Status);
    }

    // ==================================================================
    // C - filter and projection agree
    // ==================================================================

    [Fact(DisplayName =
        "D1-C: the ExpiringSoon filter and the status projection agree")]
    public async Task Client_FilterAndProjection_Agree()
    {
        var probes = await SeedProbeDocumentsAsync();

        var filtered =
            await GetClientDocumentsAsync(DocumentStatus.ExpiringSoon);

        // Nothing the filter selects may be labelled as anything else.
        foreach (var row in filtered)
        {
            Assert.Equal(DocumentStatus.ExpiringSoon, row.Status);
        }

        var all = await GetClientDocumentsAsync(null);

        // The +60d probe: inside the filter, and labelled ExpiringSoon.
        Assert.True(
            filtered.Any(x => x.Id == probes.Inside),
            $"+{InsideDays}d document is missing from the " +
            "ExpiringSoon filter.");

        Assert.Equal(
            DocumentStatus.ExpiringSoon,
            RowById(all, probes.Inside, x => x.Id, "unfiltered").Status);

        // The +91d probe: outside the filter, and labelled Active. Together
        // these two show the filter and the projection partition the same rows
        // the same way, which is the invariant the 30-day filter broke.
        Assert.False(
            filtered.Any(x => x.Id == probes.Outside),
            $"+{OutsideDays}d document was returned by the " +
            "ExpiringSoon filter.");

        Assert.Equal(
            DocumentStatus.Active,
            RowById(all, probes.Outside, x => x.Id, "unfiltered").Status);
    }

    // ==================================================================
    // D - outside the status window
    // ==================================================================

    [Fact(DisplayName =
        "D1-D: a +91 day document is excluded by status=ExpiringSoon " +
        "and is projected Active")]
    public async Task Client_ExpiringSoonFilter_ExcludesNinetyOneDayDocument()
    {
        var probes = await SeedProbeDocumentsAsync();

        var filtered =
            await GetClientDocumentsAsync(DocumentStatus.ExpiringSoon);

        Assert.False(
            filtered.Any(x => x.Id == probes.Outside),
            $"Document seeded at +{OutsideDays}d ({probes.Outside}) is " +
            "beyond the 90-day status window and must not be " +
            "selected by status=ExpiringSoon.");

        // The complement must have picked it up, and label it Active.
        var active = await GetClientDocumentsAsync(DocumentStatus.Active);

        Assert.True(
            active.Any(x => x.Id == probes.Outside),
            $"Document seeded at +{OutsideDays}d is outside the " +
            "ExpiringSoon window and must be selected by status=Active.");

        Assert.Equal(
            DocumentStatus.Active,
            RowById(active, probes.Outside, x => x.Id, "Active").Status);
    }

    // ==================================================================
    // E - the Active filter is the complement, not a 30-day complement
    // ==================================================================

    [Fact(DisplayName =
        "D1-E: status=Active does not include the +60 day " +
        "ExpiringSoon document")]
    public async Task Client_ActiveFilter_ExcludesSixtyDayDocument()
    {
        var probes = await SeedProbeDocumentsAsync();

        var active = await GetClientDocumentsAsync(DocumentStatus.Active);

        // The Active case was one of the two defective branches. It had to move
        // with the ExpiringSoon case, otherwise the two would still overlap in
        // the 30-to-90 band and contradict the projection.
        Assert.False(
            active.Any(x => x.Id == probes.Inside),
            $"Document seeded at +{InsideDays}d is projected " +
            "ExpiringSoon and must not be selected by status=Active.");
    }

    // ==================================================================
    // F - the sibling Overdue case is unaffected
    // ==================================================================

    [Fact(DisplayName =
        "D1-F: status=Overdue still selects the expired document")]
    public async Task Client_OverdueFilter_SelectsExpiredDocument()
    {
        var probes = await SeedProbeDocumentsAsync();

        var overdue = await GetClientDocumentsAsync(DocumentStatus.Overdue);

        // Overdue is expiry < now and is window-independent, so D1 must not
        // have touched it.
        Assert.True(
            overdue.Any(x => x.Id == probes.Expired),
            $"Document seeded at {ExpiredDays}d ({probes.Expired}) is " +
            "overdue and must be selected by status=Overdue.");

        Assert.Equal(
            DocumentStatus.Overdue,
            RowById(overdue, probes.Expired, x => x.Id, "Overdue")
                .Status);
    }

    // ==================================================================
    // G - the user-facing 30-day expiry list is NOT changed
    // ==================================================================

    [Fact(DisplayName =
        "D1-G: the client expiry list with days=30 still excludes a " +
        "+60 day document")]
    public async Task Client_ExpiringList_WithDays30_ExcludesSixtyDayDocument()
    {
        var probes = await SeedProbeDocumentsAsync();

        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/documents/expiring?days=30&page=1" +
                $"&pageSize={PageSize}",
                ClientA, role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<
            ApiResponse<GetExpiringDocumentsResponseDto>>(response);

        Assert.NotNull(envelope);

        var items = Assert.IsType<GetExpiringDocumentsResponseDto>(
            envelope!.Data).Items;

        // D1 widens the STATUS filter only. The user-facing expiry list keeps
        // its own caller-supplied days window, which defaults to 30, so a +60d
        // document must still be excluded here. If this ever fails, the status
        // change has leaked into the user-facing window.
        Assert.False(
            items.Any(x => x.Id == probes.Inside),
            $"Document seeded at +{InsideDays}d is still correctly " +
            "outside the 30-day user-facing expiry window, but the " +
            "expiring list returned it.");
    }

    // ==================================================================
    // H - the agent documents list behaves identically
    // ==================================================================

    [Fact(DisplayName =
        "D1-H: agent status=ExpiringSoon matches the client " +
        "semantics for +60 and +91 days")]
    public async Task Agent_ExpiringSoonFilter_MatchesClientSemantics()
    {
        var probes = await SeedProbeDocumentsAsync();

        var filtered =
            await GetAgentDocumentsAsync(DocumentStatus.ExpiringSoon);

        // +60 inside.
        Assert.True(
            filtered.Any(x => x.Id == probes.Inside),
            $"Agent: document seeded at +{InsideDays}d " +
            $"({probes.Inside}) is in the agent's portfolio but was " +
            "excluded by status=ExpiringSoon.");

        Assert.Equal(
            DocumentStatus.ExpiringSoon,
            RowById(filtered, probes.Inside, x => x.Id,
                "agent ExpiringSoon").Status);

        // +91 outside, and projected Active on the agent read model too.
        Assert.False(
            filtered.Any(x => x.Id == probes.Outside),
            $"Agent: document seeded at +{OutsideDays}d is beyond the " +
            "90-day status window and must not be selected.");

        var all = await GetAgentDocumentsAsync(null);

        Assert.Equal(
            DocumentStatus.Active,
            RowById(all, probes.Outside, x => x.Id,
                "agent unfiltered").Status);
    }

    // ==================================================================
    // Scoping - the widening must not expose anything new
    // ==================================================================

    [Fact(DisplayName =
        "D1-SCOPE-CLIENT: the widened +60 band does not leak another " +
        "company's document")]
    public async Task Client_ExpiringSoonFilter_DoesNotLeakOtherCompany()
    {
        var probes = await SeedProbeDocumentsAsync();

        var filtered =
            await GetClientDocumentsAsync(DocumentStatus.ExpiringSoon);

        // This probe is owned by Entity B and sits at +60d, so it falls
        // squarely inside the band that D1 widened from 30 to 90. Widening a
        // window is the one change that could surface a row the tenant scope
        // was previously hiding, so the tenancy predicate is asserted against
        // the newly-included band specifically.
        Assert.False(
            filtered.Any(x => x.Id == probes.OtherCompanyInside),
            $"Document at +{InsideDays}d owned by another company " +
            $"({probes.OtherCompanyInside}) leaked into the client's " +
            "ExpiringSoon results after the window was widened.");
    }

    [Fact(DisplayName =
        "D1-SCOPE-AGENT: the widened +60 band does not expose another " +
        "agent's document")]
    public async Task Agent_ExpiringSoonFilter_DoesNotLeakOtherAgent()
    {
        var probes = await SeedProbeDocumentsAsync();

        var filtered =
            await GetAgentDocumentsAsync(DocumentStatus.ExpiringSoon);

        // This probe is owned by the client's OWN entity and sits at +60d, so
        // it is inside the widened band and inside the client's tenant. Its
        // renewal task is assigned to a different agent, so portfolio scoping
        // is the only thing that can exclude it from Agent A. That makes this
        // a direct test of SqlSnippets.DocumentScopeExists, which D1 must not
        // have altered.
        Assert.False(
            filtered.Any(x => x.Id == probes.OtherAgentInside),
            $"Document at +{InsideDays}d assigned to another agent " +
            $"({probes.OtherAgentInside}) leaked into Agent A's " +
            "ExpiringSoon results after the window was widened.");
    }

    // ==================================================================
    // P10.1 / R-10 - the status=Active scope escape
    // ==================================================================

    /// <summary>
    /// P10.1 R-10 REGRESSION SUITE - CRITICAL DATA ISOLATION.
    ///
    /// THE DEFECT
    ///
    /// The <c>status=Active</c> branch appended an UNPARENTHESIZED disjunction
    /// into a WHERE clause whose conditions are joined with AND:
    ///
    ///   d.expiry_date IS NULL OR (d.expiry_date &gt; now + 90 days)
    ///
    /// SQL binds AND tighter than OR, so the emitted predicate parsed as
    ///
    ///   (scope AND is_deleted = false AND expiry IS NULL)
    ///   OR (expiry &gt; now + 90 days)
    ///
    /// The second disjunct carried neither the company scope nor the
    /// soft-delete filter, so <c>GET /api/v1/client/documents?status=Active</c>
    /// returned documents from EVERY tenant, and the agent equivalent returned
    /// documents outside the requesting agent's portfolio.
    ///
    /// WHY THE P7.1 SUITE ABOVE COULD NOT SEE IT
    ///
    /// D1-SCOPE-CLIENT and D1-SCOPE-AGENT assert isolation for
    /// <c>status=ExpiringSoon</c>, which is a single AND-conjunction and was
    /// always safe. Their scoping probes also sit at +{InsideDays}d, INSIDE the
    /// 90-day window, whereas the escaping branch fires only BEYOND +90 days.
    /// The suite therefore proved the safe sibling safe and never executed the
    /// broken one. Every probe below is at +{OutsideDays}d for that reason, and
    /// every assertion correlates by document id.
    ///
    /// ISOLATION DISCIPLINE
    ///
    /// Each probe is built so that exactly ONE rule can explain its expected
    /// outcome. The "own" probes prove the filter was not over-tightened into
    /// something that hides legitimate rows; the leak probes each isolate
    /// tenancy, portfolio scoping, or the soft-delete flag.
    /// </summary>
    private sealed record ActiveScopeProbeSet(
        Guid ClientOwn,
        Guid ClientOtherCompany,
        Guid ClientOtherCompanyDeleted,
        Guid AgentOwn,
        Guid AgentOtherAgent,
        Guid AgentOtherAgentDeleted);

    /// <summary>
    /// Plants the six +{OutsideDays}d probes the R-10 assertions need.
    ///
    /// This is a dedicated seeder rather than an extension of
    /// <see cref="SeedProbeDocumentsAsync"/> so that the P7.1 suite above, and
    /// its ten tests, stay byte-for-byte unchanged. The only capability added
    /// here is soft-delete seeding.
    /// </summary>
    private async Task<ActiveScopeProbeSet> SeedActiveScopeProbesAsync()
    {
        var clientOwn = Guid.NewGuid();
        var clientOtherCompany = Guid.NewGuid();
        var clientOtherCompanyDeleted = Guid.NewGuid();
        var agentOwn = Guid.NewGuid();
        var agentOtherAgent = Guid.NewGuid();
        var agentOtherAgentDeleted = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            var now = DateTime.UtcNow;

            // isDeleted is the only difference between a probe and its
            // soft-deleted twin, so the soft-delete flag is what any exclusion
            // can be attributed to. IsActive stays true to keep it out of play.
            void AddDocument(Guid id, Guid entityId, bool isDeleted)
            {
                db.Documents.Add(new Document
                {
                    Id = id,
                    ClientEntityId = entityId,
                    EmployeeId = null,
                    Type = DocumentType.Visa,
                    DocumentNumber = $"R10-{id:N}"[..20],
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddDays(OutsideDays),
                    FileUrl = $"https://storage.test/{id:N}",
                    FileName = $"r10-probe-{id:N}.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = isDeleted,
                    DeletedAt = isDeleted ? now : null
                });
            }

            void AddTask(Guid documentId, Guid agentId)
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

            // Client tenant probes. Company A owns Entity A; Company B owns
            // Entity B. Soft-delete is applied at the document, not the owner,
            // so CK_documents_exactly_one_owner still holds.
            AddDocument(clientOwn, TestDataSeeder.Ids.EntityA, false);
            AddDocument(clientOtherCompany, TestDataSeeder.Ids.EntityB, false);
            AddDocument(
                clientOtherCompanyDeleted,
                TestDataSeeder.Ids.EntityB, true);

            // Agent portfolio probes. All are owned by the client's OWN entity
            // and sit inside the tenant, so portfolio scoping
            // (SqlSnippets.DocumentScopeExists) is the only rule that can
            // exclude the other-agent rows from Agent A.
            AddDocument(agentOwn, TestDataSeeder.Ids.EntityA, false);
            AddDocument(agentOtherAgent, TestDataSeeder.Ids.EntityA, false);
            AddDocument(
                agentOtherAgentDeleted,
                TestDataSeeder.Ids.EntityA, true);

            AddTask(agentOwn, AgentA);
            AddTask(agentOtherAgent, AgentB);
            AddTask(agentOtherAgentDeleted, AgentB);

            db.SaveChanges();
        });

        return new ActiveScopeProbeSet(
            clientOwn,
            clientOtherCompany,
            clientOtherCompanyDeleted,
            agentOwn,
            agentOtherAgent,
            agentOtherAgentDeleted);
    }

    // ==================================================================
    // R-10 client scope
    // ==================================================================

    [Fact(DisplayName =
        "R10-C1: client status=Active returns its OWN +91 day document")]
    public async Task Client_ActiveFilter_KeepsOwnBeyondWindowDocument()
    {
        var probes = await SeedActiveScopeProbesAsync();

        var active = await GetClientDocumentsAsync(DocumentStatus.Active);

        RowById(active, probes.ClientOwn, x => x.Id, "client Active");
    }

    [Fact(DisplayName =
        "R10-C2: client status=Active does NOT return another company's " +
        "+91 day document")]
    public async Task Client_ActiveFilter_DoesNotLeakOtherCompany()
    {
        var probes = await SeedActiveScopeProbesAsync();

        var active = await GetClientDocumentsAsync(DocumentStatus.Active);

        Assert.False(
            active.Any(x => x.Id == probes.ClientOtherCompany),
            $"Document at +{OutsideDays}d owned by another company " +
            $"({probes.ClientOtherCompany}) leaked into the client's " +
            "status=Active results. The Active disjunction escaped the " +
            "company scope.");
    }

    [Fact(DisplayName =
        "R10-C3: client status=Active does NOT return a soft-deleted " +
        "document")]
    public async Task Client_ActiveFilter_ExcludesSoftDeleted()
    {
        var probes = await SeedActiveScopeProbesAsync();

        var active = await GetClientDocumentsAsync(DocumentStatus.Active);

        Assert.False(
            active.Any(x => x.Id == probes.ClientOtherCompanyDeleted),
            $"Soft-deleted document at +{OutsideDays}d " +
            $"({probes.ClientOtherCompanyDeleted}) leaked into the client's " +
            "status=Active results. The Active disjunction escaped the " +
            "soft-delete filter.");
    }

    // ==================================================================
    // R-10 agent scope
    // ==================================================================

    [Fact(DisplayName =
        "R10-A1: agent status=Active returns its OWN assigned +91 day " +
        "document")]
    public async Task Agent_ActiveFilter_KeepsOwnBeyondWindowDocument()
    {
        var probes = await SeedActiveScopeProbesAsync();

        var active = await GetAgentDocumentsAsync(DocumentStatus.Active);

        RowById(active, probes.AgentOwn, x => x.Id, "agent Active");
    }

    [Fact(DisplayName =
        "R10-A2: agent status=Active does NOT return another agent's " +
        "+91 day document")]
    public async Task Agent_ActiveFilter_DoesNotLeakOtherAgent()
    {
        var probes = await SeedActiveScopeProbesAsync();

        var active = await GetAgentDocumentsAsync(DocumentStatus.Active);

        Assert.False(
            active.Any(x => x.Id == probes.AgentOtherAgent),
            $"Document at +{OutsideDays}d assigned to another agent " +
            $"({probes.AgentOtherAgent}) leaked into Agent A's " +
            "status=Active results. The Active disjunction escaped " +
            "SqlSnippets.DocumentScopeExists.");
    }

    [Fact(DisplayName =
        "R10-A3: agent status=Active does NOT return a soft-deleted " +
        "document")]
    public async Task Agent_ActiveFilter_ExcludesSoftDeleted()
    {
        var probes = await SeedActiveScopeProbesAsync();

        var active = await GetAgentDocumentsAsync(DocumentStatus.Active);

        Assert.False(
            active.Any(x => x.Id == probes.AgentOtherAgentDeleted),
            $"Soft-deleted document at +{OutsideDays}d " +
            $"({probes.AgentOtherAgentDeleted}) leaked into Agent A's " +
            "status=Active results. The Active disjunction escaped the " +
            "soft-delete filter.");
    }
}
