using System.Net;
using System.Net.Http.Json;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Exercises the real HTTP login endpoint (POST /api/v1/auth/login)
/// through the full production pipeline: model binding, FluentValidation,
/// the MediatR handler, password verification and the account-state gates
/// (active / approved / email-verified).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class LoginTests : IntegrationTestBase
{
    private const string AgentAEmail = "agent-a@test.local";

    private const string WrongPassword = "WrongPass123!";

    private const string UnknownEmail = "unknown@test.local";

    public LoginTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private Task<HttpResponseMessage> LoginAsync(
        string email,
        string password)
        => AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

    [Fact(DisplayName =
        "Active + approved + verified + correct password returns 200")]
    public async Task ValidCredentials_Returns200()
    {
        var response = await LoginAsync(
            AgentAEmail,
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<ApiResponse<LoginResponseDto>>(
            response);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data?.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data?.RefreshToken));
        Assert.Equal(AgentAEmail, envelope.Data?.User.Email);
    }

    [Fact(DisplayName =
        "Inactive account with correct password returns 403")]
    public async Task InactiveAccount_Returns403()
    {
        var response = await LoginAsync(
            "inactive@test.local",
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Not approved account with correct password returns 403")]
    public async Task NotApprovedAccount_Returns403()
    {
        var response = await LoginAsync(
            "pending@test.local",
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Email not verified with correct password returns 403")]
    public async Task EmailNotVerified_Returns403()
    {
        var response = await LoginAsync(
            "unverified@test.local",
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Known email with wrong password returns 401")]
    public async Task WrongPassword_Returns401()
    {
        var response = await LoginAsync(
            AgentAEmail,
            WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Unknown email returns 401")]
    public async Task UnknownEmail_Returns401()
    {
        var response = await LoginAsync(
            UnknownEmail,
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}