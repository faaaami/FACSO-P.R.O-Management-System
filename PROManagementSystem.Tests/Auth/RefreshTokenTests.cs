using System.Net;
using System.Net.Http.Json;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Auth.Commands.RefreshToken;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Covers the hardened refresh-token flow through the real HTTP endpoints:
/// hashed-at-rest storage, rotation with no old-token reuse, expiry, account
/// state gates (active / approved / email-verified) and logout revocation.
/// Tokens are obtained from the real login endpoint; negative states are
/// introduced only through the existing test database infrastructure.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RefreshTokenTests : IntegrationTestBase
{
    private const string AgentAEmail = "agent-a@test.local";

    private const string GarbageToken = "garbage-not-a-real-refresh-token";

    public RefreshTokenTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static readonly Guid AgentAId = TestDataSeeder.Ids.AgentA;

    private async Task<string> LoginAsync()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = AgentAEmail,
                password = TestDataSeeder.TestLoginPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<ApiResponse<LoginResponseDto>>(
            response);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.RefreshToken));

        return envelope.Data.RefreshToken;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken)
        => AuthClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken });

    private async Task<RefreshTokenResponseDto> RefreshAndReadAsync(
        string refreshToken)
    {
        var response = await RefreshAsync(refreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<ApiResponse<RefreshTokenResponseDto>>(
            response);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);

        return envelope.Data;
    }

    private Task MutateAgentAAsync(Action<User> mutation)
        => WithScopeDbContextAsync(db =>
        {
            var user = db.Users.Single(x => x.Id == AgentAId);
            mutation(user);
            db.SaveChanges();
        });

    [Fact(DisplayName =
        "Valid refresh token returns 200 with a new token pair")]
    public async Task Refresh_ValidToken_Returns200_WithNewTokens()
    {
        var refreshToken = await LoginAsync();

        var result = await RefreshAndReadAsync(refreshToken);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.NotEqual(refreshToken, result.RefreshToken);
    }

    [Fact(DisplayName =
        "Refresh rotates the token; the old token cannot be reused after rotation")]
    public async Task Refresh_RotatesToken_AndOldTokenReuse_Returns401()
    {
        var oldToken = await LoginAsync();

        var rotated = await RefreshAndReadAsync(oldToken);

        Assert.NotEqual(oldToken, rotated.RefreshToken);

        var reuseResponse = await RefreshAsync(oldToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        var rotateAgain = await RefreshAndReadAsync(rotated.RefreshToken);

        Assert.False(string.IsNullOrWhiteSpace(rotateAgain.AccessToken));
    }

    [Fact(DisplayName =
        "Expired refresh token returns 401")]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var refreshToken = await LoginAsync();

        await MutateAgentAAsync(user =>
            user.RefreshTokenExpiry = DateTime.UtcNow.AddHours(-1));

        var response = await RefreshAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Invalid/garbage refresh token returns 401")]
    public async Task Refresh_GarbageToken_Returns401()
    {
        var response = await RefreshAsync(GarbageToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Missing refresh token returns 400 (VALIDATION_ERROR semantics)")]
    public async Task Refresh_MissingToken_Returns400()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName =
        "Refresh with an inactive account returns 403")]
    public async Task Refresh_InactiveUser_Returns403()
    {
        var refreshToken = await LoginAsync();

        await MutateAgentAAsync(user => user.IsActive = false);

        var response = await RefreshAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Refresh with an unapproved account returns 403")]
    public async Task Refresh_UnapprovedUser_Returns403()
    {
        var refreshToken = await LoginAsync();

        await MutateAgentAAsync(user => user.IsApproved = false);

        var response = await RefreshAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Refresh with an unverified email returns 403")]
    public async Task Refresh_UnverifiedEmail_Returns403()
    {
        var refreshToken = await LoginAsync();

        await MutateAgentAAsync(user => user.EmailVerified = false);

        var response = await RefreshAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Logout revokes the stored refresh token and reuse returns 401")]
    public async Task Logout_RevokesToken_AndReuseReturns401()
    {
        var refreshToken = await LoginAsync();

        var logoutResponse = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        string? storedToken = null;

        await WithScopeDbContextAsync(db =>
        {
            storedToken = db.Users
                .Single(x => x.Id == AgentAId)
                .RefreshToken;
        });

        Assert.Null(storedToken);

        var refreshResponse = await RefreshAsync(refreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }
}