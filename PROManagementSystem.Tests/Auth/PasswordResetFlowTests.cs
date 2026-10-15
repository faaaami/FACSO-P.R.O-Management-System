using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Auth.Commands.ForgotPassword;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Forgot password → emailed link → reset. Links expire after one hour and
/// work only once.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PasswordResetFlowTests : AuthFlowTestBase
{
    private const string NewPassword = "Brand-New-Password-1";

    public PasswordResetFlowTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<string> RequestResetTokenAsync(string email = AgentAEmail)
    {
        var response = await PostAnonymousAsync(
            "/api/v1/auth/forgot-password",
            new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = Emails.LatestToken(email, "reset-password");
        Assert.False(string.IsNullOrEmpty(token));

        return token!;
    }

    private Task<HttpResponseMessage> ResetAsync(string token, string password = NewPassword)
        => PostAnonymousAsync(
            "/api/v1/auth/reset-password",
            new { token, newPassword = password });

    [Fact(DisplayName =
        "Forgot password emails a link that expires in one hour")]
    public async Task Forgot_SendsLink_ExpiringInOneHour()
    {
        await RequestResetTokenAsync();

        var email = Assert.Single(Emails.To(AgentAEmail));
        Assert.Contains("1 hour", email.TextBody);

        await using var db = await CreateScopeDbContextAsync();
        var token = await db.PasswordResetTokens
            .SingleAsync(x => x.UserId == TestDataSeeder.Ids.AgentA);

        Assert.Equal(AccountTokenPurpose.PasswordReset, token.Purpose);
        Assert.Null(token.UsedAt);
        Assert.InRange(
            token.ExpiresAt - DateTime.UtcNow,
            TimeSpan.FromMinutes(59),
            TimeSpan.FromMinutes(61));
    }

    [Fact(DisplayName =
        "Resetting with the link changes the password; the old one stops working")]
    public async Task Reset_ChangesPassword()
    {
        var token = await RequestResetTokenAsync();

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(token)).StatusCode);

        await LoginAsync(AgentAEmail, NewPassword);

        var old = await LoginAttemptAsync(AgentAEmail, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    [Fact(DisplayName = "A reset link works only once")]
    public async Task Reset_IsSingleUse()
    {
        var token = await RequestResetTokenAsync();

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(token)).StatusCode);

        var second = await ResetAsync(token, "Attacker-Password-1");
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        // The first reset's password is still the valid one.
        await LoginAsync(AgentAEmail, NewPassword);
    }

    [Fact(DisplayName =
        "Two simultaneous resets with the same link: exactly one succeeds")]
    public async Task Reset_ConcurrentUse_OnlyOneSucceeds()
    {
        var token = await RequestResetTokenAsync();

        var results = await Task.WhenAll(
            ResetAsync(token, "Parallel-Password-A1"),
            ResetAsync(token, "Parallel-Password-B1"));

        Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact(DisplayName = "A reset link older than one hour is rejected")]
    public async Task Reset_Expired_IsRejected()
    {
        var token = await RequestResetTokenAsync();

        await using (var db = await CreateScopeDbContextAsync())
        {
            await db.PasswordResetTokens
                .Where(x => x.UserId == TestDataSeeder.Ids.AgentA)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
        }

        var response = await ResetAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // The password was not changed.
        await LoginAsync(AgentAEmail, Password);
    }

    [Fact(DisplayName = "Requesting a new link invalidates the previous one")]
    public async Task NewRequest_InvalidatesPreviousLink()
    {
        var first = await RequestResetTokenAsync();
        var second = await RequestResetTokenAsync();

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ResetAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(second)).StatusCode);
    }

    [Fact(DisplayName = "Resetting the password signs out existing sessions")]
    public async Task Reset_RevokesRefreshToken()
    {
        var session = await LoginAsync(AgentAEmail, Password);
        var token = await RequestResetTokenAsync();

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(token)).StatusCode);

        var refresh = await PostAnonymousAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Theory(DisplayName =
        "Unknown or inactive accounts get the same response and no email")]
    [InlineData("nobody@test.local")]
    [InlineData("inactive@test.local")]
    public async Task Forgot_UnknownOrInactive_RevealsNothing(string email)
    {
        var response = await PostAnonymousAsync(
            "/api/v1/auth/forgot-password",
            new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ForgotPasswordCommandHandler.GenericMessage,
            (await DataAsync(response))["message"]!.GetValue<string>());
        Assert.Empty(Emails.Messages);
    }

    [Fact(DisplayName =
        "A delivery failure is not revealed to the caller")]
    public async Task Forgot_EmailFailure_StillReturnsGenericResponse()
    {
        Emails.FailSends = true;

        var response = await PostAnonymousAsync(
            "/api/v1/auth/forgot-password",
            new { email = AgentAEmail });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ForgotPasswordCommandHandler.GenericMessage,
            (await DataAsync(response))["message"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "Resend verification emails an unverified user a link that verifies them")]
    public async Task ResendVerification_ThenVerify()
    {
        const string unverified = "unverified@test.local";

        var resend = await PostAnonymousAsync(
            "/api/v1/auth/resend-verification",
            new { email = unverified });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        var token = Emails.LatestToken(unverified, "verify-email");
        Assert.False(string.IsNullOrEmpty(token));

        var blocked = await LoginAttemptAsync(unverified, Password);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var verify = await PostAnonymousAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        await LoginAsync(unverified, Password);

        var reuse = await PostAnonymousAsync("/api/v1/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact(DisplayName = "Resend verification sends nothing to verified accounts")]
    public async Task ResendVerification_VerifiedAccount_SendsNothing()
    {
        var response = await PostAnonymousAsync(
            "/api/v1/auth/resend-verification",
            new { email = AgentAEmail });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Emails.Messages);
    }
}
