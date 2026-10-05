using System.Net;
using System.Text.Json.Nodes;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Client;

/// <summary>
/// Verifies the Client-scoped account surface (GET/PATCH me, change-password)
/// delegates to the shared profile/credential application handlers.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientAccountTests : IntegrationTestBase
{
    private static readonly Guid ClientUserA =
        TestDataSeeder.Ids.ClientUserA;

    public ClientAccountTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<HttpResponseMessage> ClientPatch(
        string url,
        object? body,
        Guid userId)
        => await ScopeClient.SendAsync(
            ScopePatch(url, body, userId, "Client"));

    [Fact(DisplayName =
        "Client can read their own profile via /api/v1/client/me (200)")]
    public async Task GetMe_ReturnsProfile()
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet("/api/v1/client/me", ClientUserA, "Client"));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        Assert.Equal("Client A", (string)body["data"]!["fullName"]!);
        Assert.Equal("Client", (string)body["data"]!["role"]!);
        Assert.Equal(
            TestDataSeeder.Ids.CompanyA.ToString(),
            (string)body["data"]!["clientCompanyId"]!);
    }

    [Fact(DisplayName =
        "Client can update their own profile via PATCH /api/v1/client/me (200)")]
    public async Task PatchMe_UpdatesProfile()
    {
        var response = await ClientPatch(
            "/api/v1/client/me",
            new { fullName = "Client A Updated", phone = "+971500000000" },
            ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        Assert.Equal("Client A Updated", (string)body["data"]!["fullName"]!);
        Assert.Equal("+971500000000", (string)body["data"]!["phone"]!);
    }

    [Fact(DisplayName =
        "Client can change their own password via PATCH /api/v1/client/me/change-password (200)")]
    public async Task ChangePassword_Returns200()
    {
        var response = await ClientPatch(
            "/api/v1/client/me/change-password",
            new
            {
                currentPassword = TestDataSeeder.TestLoginPassword,
                newPassword = "NewPassword456!",
                confirmPassword = "NewPassword456!"
            },
            ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);
    }
}