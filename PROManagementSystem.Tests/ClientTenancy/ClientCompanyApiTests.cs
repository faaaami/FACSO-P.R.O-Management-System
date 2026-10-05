using System.Net;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ClientTenancy;

/// <summary>
/// Week 3 Day 1: Client Company Profile + tenant isolation.
/// The tenant is ALWAYS derived from the authenticated user's
/// ClientCompanyId claim (never from a client-supplied value). Cross-company
/// resources resolve to 404, never 403. Admin and Agent behavior unverifiably
/// must remain unchanged.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientCompanyApiTests : IntegrationTestBase
{
    public ClientCompanyApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid ClientA = TestDataSeeder.Ids.ClientUserA;
    private static readonly Guid ClientB = TestDataSeeder.Ids.ClientUserB;
    private static readonly Guid Admin = TestDataSeeder.Ids.Admin;
    private static readonly Guid AgentA = TestDataSeeder.Ids.AgentA;

    private static readonly string CompanyA =
        TestDataSeeder.Ids.CompanyA.ToString();
    private static readonly string CompanyB =
        TestDataSeeder.Ids.CompanyB.ToString();

    // ------------------------------------------------------------------
    // 1. Client can retrieve own company
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "1: Client retrieves their own company (200)")]
    public async Task ClientCompany_Own_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/company", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(CompanyA, body);
        Assert.Contains("Company A", body);
    }

    // ------------------------------------------------------------------
    // 2/10. Tenant cannot be overridden by a client-supplied id
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "2+10: Client-supplied ClientCompanyId cannot override authenticated tenant")]
    public async Task ClientCompany_SuppliedId_DoesNotOverrideTenant()
    {
        // A spurious ?clientCompanyId=<CompanyB> query parameter must be
        // ignored; the tenant is the authenticated user's claim (CompanyA).
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/company?clientCompanyId={CompanyB}",
                ClientA,
                role: "Client",
                clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(CompanyA, body);
        Assert.DoesNotContain(CompanyB, body);
    }

    // ------------------------------------------------------------------
    // 3. Client can retrieve own entities
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "3: Client lists only their company's entities (200)")]
    public async Task ClientEntities_Own_OnlyOwnWorld()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/entities", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.EntityA.ToString(), body);
        Assert.DoesNotContain(TestDataSeeder.Ids.EntityB.ToString(), body);
    }

    // ------------------------------------------------------------------
    // 4. Client cannot retrieve another company's entity by ID
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "4: Client cannot read another company's entity by id (404)")]
    public async Task ClientEntityById_OthersWorld_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/entities/{TestDataSeeder.Ids.EntityB}",
                ClientA,
                role: "Client",
                clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "4b: Client reads their own entity by id (200)")]
    public async Task ClientEntityById_Own_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/entities/{TestDataSeeder.Ids.EntityA}",
                ClientA,
                role: "Client",
                clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName =
        "9: Client entity by id - nonexistent resource returns 404")]
    public async Task ClientEntityById_Unknown_Returns404()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/client/entities/{TestDataSeeder.Ids.Unknown}",
                ClientA,
                role: "Client",
                clientCompanyId: CompanyA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // 5. Client can retrieve own contacts (tenant isolation via list)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "5: Client lists only their company's contacts (200)")]
    public async Task ClientContacts_Own_OnlyOwnWorld()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/contacts", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(ClientA.ToString(), body);
        Assert.DoesNotContain(ClientB.ToString(), body);
    }

    [Fact(DisplayName =
        "6: Client cannot see contacts belonging to another company")]
    public async Task ClientContacts_OthersWorld_Excluded()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/contacts", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ClientB.ToString(), body);
    }

    // ------------------------------------------------------------------
    // 7/8. Active contract
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "7: Client retrieves their own active contract (200)")]
    public async Task ClientActiveContract_Own_Returns200()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/contracts/active", ClientA,
                role: "Client", clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(TestDataSeeder.Ids.ContractA.ToString(), body);
    }

    // ------------------------------------------------------------------
    // 11. Admin cross-company behavior remains unchanged
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "11: Admin can still read company (admin) data cross-company (200)")]
    public async Task Admin_CrossCompany_Unchanged()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/admin/clients/{CompanyB}",
                Admin,
                role: "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Company B", body);
    }

    [Fact(DisplayName =
        "11b: Admin list endpoint unchanged (200)")]
    public async Task Admin_List_Unchanged()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/admin/clients", Admin, role: "Admin"));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    // 12. Agent behavior remains unchanged
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "12: Agent task-linked endpoints still return 200")]
    public async Task Agent_OwnTask_Unchanged()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                $"/api/v1/agent/tasks/{TestDataSeeder.Ids.TaskA}",
                AgentA,
                role: "Agent",
                clientCompanyId: CompanyA));

        await EnsureOk(response, HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    // 13/14. Role gates on the new Client endpoints (AuthClient real JWT)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "13: Agent JWT on Client endpoint returns 403")]
    public async Task AgentToken_OnClientEndpoint_Returns403()
    {
        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA, "Agent",
            "agent-a@test.local", "Agent A",
            clientCompanyId: CompanyA);

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/client/company", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "13b: Admin JWT on Client endpoint returns 403")]
    public async Task AdminToken_OnClientEndpoint_Returns403()
    {
        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.Admin, "Admin",
            "admin@test.local", "System Admin");

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/client/company", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "14: Unauthenticated request to Client endpoint returns 401")]
    public async Task NoToken_OnClientEndpoint_Returns401()
    {
        var response = await AuthClient.GetAsync(
            "/api/v1/client/company");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // 8. Client cannot retrieve another company's active contract
    //    (a tenant with no active contract yields 404 — and never leaks
    //    another company's contract)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "8: Client without an active contract for their tenant returns 404")]
    public async Task ClientActiveContract_OthersWorld_Returns404()
    {
        // A tenant (Unknown) with no seeded company or contract must get
        // 404, and never another company's contract.
        var unknownCompany = TestDataSeeder.Ids.Unknown.ToString();
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/contracts/active", ClientB,
                role: "Client", clientCompanyId: unknownCompany));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}