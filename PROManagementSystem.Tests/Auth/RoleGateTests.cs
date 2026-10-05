using System.Net;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Verifies the Day 6 C1 + C2 role gates through the real JWT pipeline:
///  - C1: every /api/v1/agent/* controller is Agent-only (Agent 200,
///    Client 403, Admin 403, no token 401).
///  - C2: the seven Admin modules (ServiceContracts, GovFeeDisbursements,
///    PaymentOrders, RetainerInvoices, ServiceFeeInvoices, ServiceRequests,
///    Dashboard) are Admin-only (Agent 403, Client 403, Admin 200).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RoleGateTests : IntegrationTestBase
{
    public RoleGateTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    public static IEnumerable<object[]> AdminModules()
    {
        yield return new object[]
        {
            "ServiceContracts",
            $"/api/v1/clients/{TestDataSeeder.Ids.CompanyA}/contracts"
        };
        yield return new object[]
        {
            "GovFeeDisbursements",
            "/api/v1/gov-fee-disbursements"
        };
        yield return new object[]
        {
            "PaymentOrders",
            "/api/v1/payment-orders"
        };
        yield return new object[]
        {
            "RetainerInvoices",
            "/api/v1/retainer-invoices"
        };
        yield return new object[]
        {
            "ServiceFeeInvoices",
            "/api/v1/service-fee-invoices"
        };
        yield return new object[]
        {
            "ServiceRequests",
            "/api/v1/service-requests"
        };
        yield return new object[]
        {
            "Dashboard",
            "/api/v1/dashboard/summary"
        };
    }

    private string AgentToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

    private string ClientToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            "client-a@test.local",
            "Client A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

    private string AdminToken()
        => JwtHelper.MintToken(
            TestDataSeeder.Ids.Admin,
            "Admin",
            "admin@test.local",
            "System Admin");

    [Theory(DisplayName =
        "Agent JWT on an Agent endpoint succeeds (200)")]
    [InlineData("/api/v1/agent/me")]
    [InlineData("/api/v1/agent/tasks")]
    public async Task AgentToken_OnAgentEndpoint_Returns200(string url)
    {
        var response = await AuthClient.SendAsync(
            AuthGet(url, AgentToken()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory(DisplayName =
        "Admin JWT on an Agent endpoint returns 403 (C1 gate)")]
    [InlineData("/api/v1/agent/me")]
    [InlineData("/api/v1/agent/tasks")]
    public async Task AdminToken_OnAgentEndpoint_Returns403(string url)
    {
        var response = await AuthClient.SendAsync(
            AuthGet(url, AdminToken()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory(DisplayName =
        "Agent JWT on each Admin-only module returns 403 (C2 gate)")]
    [MemberData(nameof(AdminModules))]
    public async Task AgentToken_OnAdminModule_Returns403(
        string module, string url)
    {
        var response = await AuthClient.SendAsync(
            AuthGet(url, AgentToken()));

        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden,
            $"{module} unexpectedly returned {response.StatusCode}");
    }

    [Theory(DisplayName =
        "Client JWT on each Admin-only module returns 403 (C2 gate)")]
    [MemberData(nameof(AdminModules))]
    public async Task ClientToken_OnAdminModule_Returns403(
        string module, string url)
    {
        var response = await AuthClient.SendAsync(
            AuthGet(url, ClientToken()));

        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden,
            $"{module} unexpectedly returned {response.StatusCode}");
    }

    [Theory(DisplayName =
        "Admin JWT on each Admin-only module succeeds (200)")]
    [MemberData(nameof(AdminModules))]
    public async Task AdminToken_OnAdminModule_Returns200(
        string module, string url)
    {
        var response = await AuthClient.SendAsync(
            AuthGet(url, AdminToken()));

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{module} unexpectedly returned {response.StatusCode}");
    }

    [Fact(DisplayName =
        "No token on an Admin-only module returns 401")]
    public async Task NoToken_OnAdminModule_Returns401()
    {
        var response = await AuthClient.GetAsync(
            "/api/v1/gov-fee-disbursements");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent JWT on another Agent endpoint succeeds (200)")]
    public async Task AgentToken_OnAgentClients_Returns200()
    {
        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/clients", AgentToken()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}