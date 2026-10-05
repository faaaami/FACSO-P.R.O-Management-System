using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Billing;

/// <summary>
/// P9.1 F7: mutation coverage for the Admin Government Fee lifecycle.
///
/// The gap this closes is one of coverage, not behaviour. Every existing
/// gov-fee test exercised a READ: the list endpoint, the per-company tenancy
/// predicate, the status filter, page-size validation and the role gate
/// (BillingReadTenancyTests, ClientBillingApiTests, ValidationSemanticsTests,
/// RoleGateTests). The two mutating endpoints — POST
/// /api/v1/gov-fee-disbursements and PATCH
/// /api/v1/gov-fee-disbursements/{id}/status — had none, while their retainer and
/// service-fee twins carry seven concurrency tests each. The state machine was
/// therefore correct but unverified: nothing would have caught a regression that
/// let a disbursement skip Reimbursed, go backwards, or be moved after the money
/// came back.
///
/// No production code is touched by this file. The state machine under test is
/// UpdateGovFeeDisbursementStatusCommandHandler, read to establish what the
/// transitions actually are rather than assuming them:
///
///   PaidByFirm       -> InvoicedToClient   legal, stamps InvoicedAt
///   InvoicedToClient -> Reimbursed         legal, stamps ReimbursedAt
///   Reimbursed       -> anything           refused, terminal
///   any other target from any state        refused
///
/// That is TWO legal transitions, not four. An earlier audit pass reported four;
/// it counted the create and the GET-shaped surfaces. The tests below are written
/// against the handler, and this comment is here so the number is not
/// mis-transcribed again.
///
/// A new disbursement always starts at PaidByFirm with PaidByFirmAt stamped —
/// the firm has paid and nothing has been invoiced yet — so the state machine is
/// linear and every path into it is either the seed or a create.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class GovFeeDisbursementMutationTests : IntegrationTestBase
{
    public GovFeeDisbursementMutationTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;
    private static readonly Guid ClientUserA = TestDataSeeder.Ids.ClientUserA;

    private static readonly Guid CompanyA = TestDataSeeder.Ids.CompanyA;
    private static readonly Guid CompanyB = TestDataSeeder.Ids.CompanyB;

    /// <summary>Seeded PaidByFirm on company A — the start of the chain.</summary>
    private static readonly Guid DisbursementA =
        TestDataSeeder.Ids.GovFeeDisbursementA;

    /// <summary>Seeded InvoicedToClient on company B — the middle.</summary>
    private static readonly Guid DisbursementB =
        TestDataSeeder.Ids.GovFeeDisbursementB;

    /// <summary>Seeded Reimbursed on company A — the terminal state.</summary>
    private static readonly Guid DisbursementC =
        TestDataSeeder.Ids.GovFeeDisbursementC;

    /// <summary>TaskA belongs to company A; TaskB to company B.</summary>
    private static readonly Guid TaskA = TestDataSeeder.Ids.TaskA;
    private static readonly Guid TaskB = TestDataSeeder.Ids.TaskB;

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string StatusUrl(Guid id)
        => $"/api/v1/gov-fee-disbursements/{id}/status";

    private async Task<(HttpStatusCode code, string body)> PatchStatusAsync(
        Guid id,
        GovFeeDisbursementStatus status,
        Guid? asUser = null,
        string role = "Admin")
    {
        var response = await ScopeClient.SendAsync(
            ScopePatch(
                StatusUrl(id),
                new { status = status.ToString() },
                asUser ?? Admin,
                role));

        return (
            response.StatusCode,
            await response.Content.ReadAsStringAsync());
    }

    private Task<(HttpStatusCode code, string body)> CreateAsync(
        object body,
        Guid? asUser = null,
        string role = "Admin")
    {
        var response = ScopePost(
            "/api/v1/gov-fee-disbursements",
            body,
            asUser ?? Admin,
            role);

        return SendAsync(response);
    }

    private async Task<(HttpStatusCode code, string body)> SendAsync(
        HttpRequestMessage request)
    {
        var response = await ScopeClient.SendAsync(request);

        return (
            response.StatusCode,
            await response.Content.ReadAsStringAsync());
    }

    private static object NewDisbursement(
        Guid? companyId = null,
        Guid? taskId = null,
        string description = "Residence visa government fee",
        decimal amount = 5000m,
        string currency = "AED")
        => new
        {
            clientCompanyId = companyId ?? CompanyA,
            renewalTaskId = taskId,
            feeDescription = description,
            governmentReference = (string?)null,
            amount,
            currency,
            notes = (string?)null
        };

    private async Task<(GovFeeDisbursementStatus status, DateTime? paidAt,
        DateTime? invoicedAt, DateTime? reimbursedAt)> ReadAsync(Guid id)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.GovFeeDisbursements.SingleAsync(x => x.Id == id);

            return (row.Status, row.PaidByFirmAt, row.InvoicedAt,
                row.ReimbursedAt);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // A. Create
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F7-A1. Admin can create a disbursement (201)")]
    public async Task Admin_CanCreateADisbursement()
    {
        var (code, _) = await CreateAsync(NewDisbursement());

        Assert.Equal(HttpStatusCode.Created, code);
    }

    [Fact(DisplayName =
        "F7-A2. A created disbursement starts at PaidByFirm with PaidByFirmAt " +
        "stamped and no later timestamps")]
    public async Task ACreatedDisbursement_StartsAtPaidByFirm()
    {
        var (code, body) = await CreateAsync(NewDisbursement());
        var compact = body.Replace(" ", string.Empty);

        Assert.Equal(HttpStatusCode.Created, code);

        // The only entry point into the state machine, and the reason the machine
        // is linear: the firm has paid, so the row starts there and InvoicedAt and
        // ReimbursedAt must both be null rather than zero.
        Assert.Contains("\"status\":\"PaidByFirm\"", compact);
        Assert.Contains("\"paidByFirmAt\":", compact);
        Assert.Contains("\"invoicedAt\":null", compact);
        Assert.Contains("\"reimbursedAt\":null", compact);
    }

    [Fact(DisplayName =
        "F7-A3. A created disbursement is persisted for its company")]
    public async Task ACreatedDisbursement_IsPersisted()
    {
        var (code, body) = await CreateAsync(
            NewDisbursement(description: "  Emirates ID renewal fee  "));
        var compact = body.Replace(" ", string.Empty);

        Assert.Equal(HttpStatusCode.Created, code);

        var id = ExtractGuid(compact, "\"id\":\"");
        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.GovFeeDisbursements.SingleAsync(x => x.Id == id);

            Assert.Equal(CompanyA, row.ClientCompanyId);
            Assert.Equal(5000m, row.Amount);
            Assert.Equal("AED", row.Currency);

            // Trimmed on the way in, as the handler documents.
            Assert.Equal("Emirates ID renewal fee", row.FeeDescription);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "F7-A4. A created disbursement can then be driven all the way to " +
        "Reimbursed")]
    public async Task ACreatedDisbursement_CanCompleteTheChain()
    {
        // The end-to-end proof that create really does enter the state machine at
        // its first state and not somewhere unreachable: create, then the two
        // legal transitions, and the chain completes.
        var (_, body) = await CreateAsync(NewDisbursement());
        var id = ExtractGuid(body.Replace(" ", string.Empty), "\"id\":\"");

        var (first, _) = await PatchStatusAsync(
            id, GovFeeDisbursementStatus.InvoicedToClient);
        var (second, _) = await PatchStatusAsync(
            id, GovFeeDisbursementStatus.Reimbursed);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.OK, second);

        var (status, _, _, reimbursedAt) = await ReadAsync(id);
        Assert.Equal(GovFeeDisbursementStatus.Reimbursed, status);
        Assert.NotNull(reimbursedAt);
    }

    [Theory(DisplayName =
        "F7-A5. Invalid create input is refused (400)")]
    [InlineData("", 5000, "AED")]
    [InlineData("A fee", -1, "AED")]
    [InlineData("A fee", 5000, "")]
    public async Task InvalidCreateInput_IsRefused(
        string description,
        decimal amount,
        string currency)
    {
        var (code, body) = await CreateAsync(
            NewDisbursement(
                description: description, amount: amount, currency: currency));

        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("VALIDATION_ERROR", body);
    }

    [Fact(DisplayName =
        "F7-A6. A create for an unknown company is refused (404)")]
    public async Task ACreateForAnUnknownCompany_IsRefused()
    {
        var (code, _) = await CreateAsync(
            NewDisbursement(companyId: TestDataSeeder.Ids.Unknown));

        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    // ------------------------------------------------------------------
    // B. Legal transitions — the complete set, both of them
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F7-B1. PaidByFirm -> InvoicedToClient succeeds and stamps InvoicedAt")]
    public async Task PaidByFirm_ToInvoicedToClient_Succeeds()
    {
        var (code, body) = await PatchStatusAsync(
            DisbursementA,
            GovFeeDisbursementStatus.InvoicedToClient);
        var compact = body.Replace(" ", string.Empty);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("\"status\":\"InvoicedToClient\"", compact);

        var (status, _, invoicedAt, reimbursedAt) =
            await ReadAsync(DisbursementA);

        Assert.Equal(GovFeeDisbursementStatus.InvoicedToClient, status);
        Assert.NotNull(invoicedAt);
        Assert.Null(reimbursedAt);
    }

    [Fact(DisplayName =
        "F7-B2. InvoicedToClient -> Reimbursed succeeds and stamps ReimbursedAt")]
    public async Task InvoicedToClient_ToReimbursed_Succeeds()
    {
        var (code, body) = await PatchStatusAsync(
            DisbursementB,
            GovFeeDisbursementStatus.Reimbursed);
        var compact = body.Replace(" ", string.Empty);

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("\"status\":\"Reimbursed\"", compact);

        var (status, _, invoicedAt, reimbursedAt) =
            await ReadAsync(DisbursementB);

        Assert.Equal(GovFeeDisbursementStatus.Reimbursed, status);
        Assert.NotNull(reimbursedAt);

        // The earlier stamp survives; the chain accumulates rather than
        // overwriting.
        Assert.NotNull(invoicedAt);
    }

    [Fact(DisplayName =
        "F7-B3a. PaidByFirm -> InvoicedToClient writes a " +
        "GovFeeDisbursementStatusUpdated audit row naming the new status")]
    public async Task TheFirstTransition_WritesAnAuditRow()
    {
        await PatchStatusAsync(
            DisbursementA,
            GovFeeDisbursementStatus.InvoicedToClient);

        await AssertStatusAuditAsync(
            DisbursementA,
            GovFeeDisbursementStatus.InvoicedToClient);
    }

    [Fact(DisplayName =
        "F7-B3b. InvoicedToClient -> Reimbursed writes a " +
        "GovFeeDisbursementStatusUpdated audit row naming the new status")]
    public async Task TheSecondTransition_WritesAnAuditRow()
    {
        await PatchStatusAsync(
            DisbursementB,
            GovFeeDisbursementStatus.Reimbursed);

        await AssertStatusAuditAsync(
            DisbursementB,
            GovFeeDisbursementStatus.Reimbursed);
    }

    private async Task AssertStatusAuditAsync(
        Guid id,
        GovFeeDisbursementStatus target)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            var row = await db.AuditLogs
                .Where(x =>
                    x.EntityId == id &&
                    x.Action == "GovFeeDisbursementStatusUpdated")
                .FirstAsync();

            Assert.Equal("GovFeeDisbursement", row.EntityType);
            Assert.Equal(Admin, row.UserId);
            Assert.Contains(target.ToString(), row.Description!);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "F7-B4. Both legal transitions in sequence reach Reimbursed")]
    public async Task BothLegalTransitions_InSequence_ReachReimbursed()
    {
        var (first, _) = await PatchStatusAsync(
            DisbursementA, GovFeeDisbursementStatus.InvoicedToClient);
        var (second, _) = await PatchStatusAsync(
            DisbursementA, GovFeeDisbursementStatus.Reimbursed);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.OK, second);

        var (status, paidAt, invoicedAt, reimbursedAt) =
            await ReadAsync(DisbursementA);

        Assert.Equal(GovFeeDisbursementStatus.Reimbursed, status);
        Assert.NotNull(paidAt);
        Assert.NotNull(invoicedAt);
        Assert.NotNull(reimbursedAt);
    }

    // ------------------------------------------------------------------
    // C. Illegal transitions
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "F7-C1. PaidByFirm refuses every target except InvoicedToClient (409)")]
    [InlineData(GovFeeDisbursementStatus.PaidByFirm)]
    [InlineData(GovFeeDisbursementStatus.Reimbursed)]
    public async Task PaidByFirm_RefusesIllegalTargets(
        GovFeeDisbursementStatus target)
    {
        // PaidByFirm -> Reimbursed is the one that matters commercially: it would
        // record the client having reimbursed a fee they were never invoiced for.
        var (code, body) = await PatchStatusAsync(DisbursementA, target);

        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Contains("CONFLICT", body);
    }

    [Theory(DisplayName =
        "F7-C2. InvoicedToClient refuses every target except Reimbursed (409)")]
    [InlineData(GovFeeDisbursementStatus.PaidByFirm)]
    [InlineData(GovFeeDisbursementStatus.InvoicedToClient)]
    public async Task InvoicedToClient_RefusesIllegalTargets(
        GovFeeDisbursementStatus target)
    {
        // Includes the backwards move, which is the transition most likely to be
        // reached by a client of the API that assumed status was freely settable.
        var (code, _) = await PatchStatusAsync(DisbursementB, target);

        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Theory(DisplayName =
        "F7-C3. Reimbursed is terminal and refuses every target (409)")]
    [InlineData(GovFeeDisbursementStatus.PaidByFirm)]
    [InlineData(GovFeeDisbursementStatus.InvoicedToClient)]
    [InlineData(GovFeeDisbursementStatus.Reimbursed)]
    public async Task Reimbursed_RefusesEveryTarget(
        GovFeeDisbursementStatus target)
    {
        // The money has already come back. Nothing may move the row off that.
        var (code, body) = await PatchStatusAsync(DisbursementC, target);

        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Contains("reimbursed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "F7-C4. A repeated legal transition is refused once the state has moved " +
        "on (409)")]
    public async Task ARepeatedTransition_IsRefused()
    {
        // A -> InvoicedToClient is legal once. Asking again from InvoicedToClient
        // is no longer the same transition, and the machine refuses it.
        var (first, _) = await PatchStatusAsync(
            DisbursementA, GovFeeDisbursementStatus.InvoicedToClient);
        var (second, _) = await PatchStatusAsync(
            DisbursementA, GovFeeDisbursementStatus.InvoicedToClient);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.Conflict, second);
    }

    [Fact(DisplayName =
        "F7-C5. A refused transition leaves the status and every timestamp " +
        "exactly as they were")]
    public async Task ARefusedTransition_WritesNothing()
    {
        var before = await ReadAsync(DisbursementA);

        await PatchStatusAsync(
            DisbursementA, GovFeeDisbursementStatus.Reimbursed);

        var after = await ReadAsync(DisbursementA);

        Assert.Equal(before.status, after.status);
        Assert.Equal(before.paidAt, after.paidAt);
        Assert.Equal(before.invoicedAt, after.invoicedAt);
        Assert.Equal(before.reimbursedAt, after.reimbursedAt);
    }

    [Fact(DisplayName =
        "F7-C6. A refused transition writes no audit row")]
    public async Task ARefusedTransition_WritesNoAuditRow()
    {
        var db = await CreateScopeDbContextAsync();
        var before = await db.AuditLogs.CountAsync(
            x => x.EntityId == DisbursementA);
        await db.DisposeAsync();

        await PatchStatusAsync(
            DisbursementA, GovFeeDisbursementStatus.Reimbursed);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                before,
                await db2.AuditLogs.CountAsync(x => x.EntityId == DisbursementA));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "F7-C7. A transition on a missing disbursement is refused (404)")]
    public async Task AMissingDisbursement_IsRefused()
    {
        var (code, _) = await PatchStatusAsync(
            TestDataSeeder.Ids.Unknown,
            GovFeeDisbursementStatus.InvoicedToClient);

        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact(DisplayName =
        "F7-C8. The create validator still refuses a negative amount, so a " +
        "refused create persists nothing")]
    public async Task ARefusedCreate_PersistsNothing()
    {
        var db = await CreateScopeDbContextAsync();
        var before = await db.GovFeeDisbursements.CountAsync();
        await db.DisposeAsync();

        await CreateAsync(NewDisbursement(amount: -1m));

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(before, await db2.GovFeeDisbursements.CountAsync());
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // D. Authorization — both mutations are Admin-only
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "F7-D1. A non-Admin cannot create a disbursement (403)")]
    [InlineData("Agent")]
    [InlineData("Client")]
    public async Task ANonAdmin_CannotCreate(string role)
    {
        var (code, _) = await CreateAsync(
            NewDisbursement(),
            asUser: role == "Agent" ? AgentA : ClientUserA,
            role);

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Theory(DisplayName =
        "F7-D2. A non-Admin cannot change a status (403)")]
    [InlineData("Agent")]
    [InlineData("Client")]
    public async Task ANonAdmin_CannotChangeStatus(string role)
    {
        // Even a legal transition. The role gate is on the endpoint, not on which
        // transition is being asked for.
        var (code, _) = await PatchStatusAsync(
            DisbursementA,
            GovFeeDisbursementStatus.InvoicedToClient,
            asUser: role == "Agent" ? AgentA : ClientUserA,
            role);

        Assert.Equal(HttpStatusCode.Forbidden, code);
    }

    [Fact(DisplayName =
        "F7-D3. A refused role persists nothing and moves nothing")]
    public async Task ARefusedRole_WritesNothing()
    {
        var before = await ReadAsync(DisbursementA);

        var db = await CreateScopeDbContextAsync();
        var auditsBefore = await db.AuditLogs.CountAsync(
            x => x.EntityId == DisbursementA);
        await db.DisposeAsync();

        await CreateAsync(
            NewDisbursement(), asUser: AgentA, role: "Agent");
        await PatchStatusAsync(
            DisbursementA,
            GovFeeDisbursementStatus.InvoicedToClient,
            asUser: AgentA,
            role: "Agent");
        await PatchStatusAsync(
            DisbursementA,
            GovFeeDisbursementStatus.InvoicedToClient,
            asUser: ClientUserA,
            role: "Client");

        var after = await ReadAsync(DisbursementA);

        Assert.Equal(before.status, after.status);
        Assert.Equal(before.invoicedAt, after.invoicedAt);

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(
                auditsBefore,
                await db2.AuditLogs.CountAsync(
                    x => x.EntityId == DisbursementA));
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "F7-D4. An unauthenticated caller is refused (401) on both mutations")]
    public async Task AnUnauthenticatedCaller_IsRefused()
    {
        var (createCode, _) = await SendAsync(new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/gov-fee-disbursements")
        {
            Content = System.Net.Http.Json.JsonContent.Create(NewDisbursement())
        });

        var (statusCode, _) = await SendAsync(new HttpRequestMessage(
            HttpMethod.Patch, StatusUrl(DisbursementA))
        {
            Content = System.Net.Http.Json.JsonContent.Create(
                new { status = "InvoicedToClient" })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, createCode);
        Assert.Equal(HttpStatusCode.Unauthorized, statusCode);
    }

    // ------------------------------------------------------------------
    // E. Company ownership on the create's optional task link
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F7-E1. A disbursement may be linked to a task belonging to its own " +
        "company (201)")]
    public async Task ALinkedTaskFromTheSameCompany_IsAccepted()
    {
        // TaskA belongs to company A, which is what this create names.
        var (code, _) = await CreateAsync(
            NewDisbursement(companyId: CompanyA, taskId: TaskA));

        Assert.Equal(HttpStatusCode.Created, code);
    }

    [Fact(DisplayName =
        "F7-E2. A disbursement may omit the task link entirely (201)")]
    public async Task AnOmittedTaskLink_IsAccepted()
    {
        // RenewalTaskId is nullable by design: a government fee can be raised
        // outside a renewal.
        var (code, _) = await CreateAsync(NewDisbursement());

        Assert.Equal(HttpStatusCode.Created, code);
    }

    [Fact(DisplayName =
        "F7-E3. Linking a task from another company is refused (409)")]
    public async Task ATaskFromAnotherCompany_IsRefused()
    {
        // TaskB belongs to company B. Naming company A with TaskB would attach
        // one company's renewal to another company's fee, so the handler refuses.
        var (code, body) = await CreateAsync(
            NewDisbursement(companyId: CompanyA, taskId: TaskB));

        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Contains("does not belong", body);
    }

    [Fact(DisplayName =
        "F7-E4. A refused cross-company link persists nothing")]
    public async Task ARefusedCrossCompanyLink_PersistsNothing()
    {
        var db = await CreateScopeDbContextAsync();
        var before = await db.GovFeeDisbursements.CountAsync();
        await db.DisposeAsync();

        await CreateAsync(
            NewDisbursement(companyId: CompanyA, taskId: TaskB));

        var db2 = await CreateScopeDbContextAsync();
        try
        {
            Assert.Equal(before, await db2.GovFeeDisbursements.CountAsync());
        }
        finally
        {
            await db2.DisposeAsync();
        }
    }

    [Fact(DisplayName =
        "F7-E5. Linking an unknown task is refused (404)")]
    public async Task AnUnknownTaskLink_IsRefused()
    {
        var (code, _) = await CreateAsync(
            NewDisbursement(
                companyId: CompanyA,
                taskId: TestDataSeeder.Ids.Unknown));

        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    [Fact(DisplayName =
        "F7-E6. Linking a soft-deleted task is refused (404)")]
    public async Task ASoftDeletedTaskLink_IsRefused()
    {
        await WithScopeDbContextAsync(db =>
        {
            var task = db.RenewalTasks.Single(x => x.Id == TaskA);
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var (code, _) = await CreateAsync(
            NewDisbursement(companyId: CompanyA, taskId: TaskA));

        Assert.Equal(HttpStatusCode.NotFound, code);
    }

    // ------------------------------------------------------------------
    // F. The mutations coexist with the existing read surface
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "F7-F1. A disbursement created here is visible to the Admin list and " +
        "detail endpoints")]
    public async Task ANewDisbursement_IsVisibleToTheReadSurface()
    {
        // The mutation tests would pass even if the created row were invisible to
        // every existing read path, which is not the same as the feature working.
        var (_, body) = await CreateAsync(NewDisbursement());
        var id = ExtractGuid(body.Replace(" ", string.Empty), "\"id\":\"");

        var detail = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/gov-fee-disbursements/{id}",
                Admin,
                "Admin"));

        await EnsureOk(detail, HttpStatusCode.OK);
        Assert.Contains(
            "\"status\":\"PaidByFirm\"",
            (await detail.Content.ReadAsStringAsync())
                .Replace(" ", string.Empty));

        var list = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/gov-fee-disbursements?clientCompanyId={CompanyA}",
                Admin,
                "Admin"));

        await EnsureOk(list, HttpStatusCode.OK);
        Assert.Contains(
            id.ToString(),
            await list.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Pulls a guid out of a compact JSON body by the quoted key, so a test can
    /// act on the row the API just created rather than on a row it made itself.
    /// </summary>
    private static Guid ExtractGuid(string compactJson, string quotedKey)
    {
        var start = compactJson.IndexOf(quotedKey, StringComparison.Ordinal)
            + quotedKey.Length;
        var end = compactJson.IndexOf('"', start);

        return Guid.Parse(compactJson[start..end]);
    }
}
