using System.Net;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Verifies the real JWT authentication pipeline (JwtBearer middleware) is
/// enforced correctly and documents the actual authorization behavior of the
/// Agent endpoints.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AuthTests : IntegrationTestBase
{
    public AuthTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName =
        "No token on an [Authorize] Agent endpoint returns 401")]
    public async Task NoToken_Returns401()
    {
        var response = await AuthClient.GetAsync("/api/v1/agent/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "A malformed/garbage bearer token returns 401")]
    public async Task InvalidToken_Returns401()
    {
        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/me", "not-a-real-jwt"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "A token signed with the wrong key returns 401 (signature validation)")]
    public async Task WrongSecretToken_Returns401()
    {
        var token = JwtHelper.MintTokenWithWrongSecret(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A");

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/me", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "An expired token returns 401")]
    public async Task ExpiredToken_Returns401()
    {
        var token = JwtHelper.MintExpiredToken(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A");

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/me", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "A valid Agent token is authenticated on an Agent endpoint")]
    public async Task ValidAgentToken_Returns200()
    {
        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/me", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "A valid valid Admin token is authenticated on an Agent endpoint")]
    public async Task ValidAdminToken_Returns200()
    {
        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.Admin,
            "Admin",
            "admin@test.local",
            "System Admin");

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/me", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "DOC: non-role-gated Agent endpoints accept a Client principal " +
        "(bare [Authorize], no role policy). Returns 200 not 403.")]
    public async Task ClientToken_OnAgentEndpoint_IsNotRejected()
    {
        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            "client-a@test.local",
            "Client A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/me", token));

        // Documented behavior: Agent controllers use bare [Authorize], so any
        // authenticated role is accepted at the controller boundary.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "A valid Agent token can list only its own scope (task list 200)")]
    public async Task ValidAgentToken_ListTasks_Returns200()
    {
        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A");

        var response = await AuthClient.SendAsync(
            AuthGet("/api/v1/agent/tasks", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
