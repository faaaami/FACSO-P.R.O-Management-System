using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Clients are created by staff and invited by email to set their own
/// password. Public self-registration no longer exists.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClientInvitationFlowTests : AuthFlowTestBase
{
    private const string ContactEmail = "invited-contact@test.local";
    private const string ContactPassword = "Invited-Contact-Pw1";

    private static readonly string CompanyAContacts =
        $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}/contacts";

    public ClientInvitationFlowTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<Guid> InviteAsAdminAsync(string email = ContactEmail)
    {
        var admin = await LoginAsync(AdminEmail, Password);

        var response = await SendAsync(
            HttpMethod.Post,
            CompanyAContacts,
            admin.AccessToken,
            new { fullName = "Invited Contact", email, phone = "+971500000001" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await DataAsync(response);
        Assert.True(data["invitationSent"]!.GetValue<bool>());

        return data["id"]!.GetValue<Guid>();
    }

    private Task<HttpResponseMessage> AcceptAsync(string? token, string password = ContactPassword)
        => PostAnonymousAsync(
            "/api/v1/auth/accept-invitation",
            new { token, newPassword = password });

    private async Task ApproveAsync(Guid contactId)
    {
        var admin = await LoginAsync(AdminEmail, Password);

        var response = await SendAsync(
            HttpMethod.Patch,
            $"{CompanyAContacts}/{contactId}/approval",
            admin.AccessToken,
            new { approved = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Public self-registration has been removed")]
    public async Task Register_EndpointIsGone()
    {
        var response = await PostAnonymousAsync(
            "/api/v1/auth/register",
            new
            {
                fullName = "Self Registered",
                email = "self-registered@test.local",
                password = ContactPassword
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var db = await CreateScopeDbContextAsync();
        Assert.False(await db.Users.AnyAsync(x => x.Email == "self-registered@test.local"));
    }

    [Fact(DisplayName =
        "An invited contact gets an email, has no usable password and is unverified")]
    public async Task Invite_SendsEmail_AndAccountIsNotUsableYet()
    {
        var contactId = await InviteAsAdminAsync();

        var email = Assert.Single(Emails.To(ContactEmail));
        Assert.Contains("Company A", email.TextBody);
        Assert.NotNull(Emails.LatestToken(ContactEmail, "accept-invitation"));

        await using (var db = await CreateScopeDbContextAsync())
        {
            var user = await db.Users.SingleAsync(x => x.Id == contactId);
            Assert.Equal(UserRole.Client, user.Role);
            Assert.False(user.EmailVerified);
            Assert.False(user.IsApproved);

            var token = await db.PasswordResetTokens.SingleAsync(x => x.UserId == contactId);
            Assert.Equal(AccountTokenPurpose.Invitation, token.Purpose);
            Assert.InRange(
                token.ExpiresAt - DateTime.UtcNow,
                TimeSpan.FromHours(71),
                TimeSpan.FromHours(73));
        }

        // Nobody knows the placeholder password, so there is nothing to sign in with.
        var login = await LoginAttemptAsync(ContactEmail, ContactPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact(DisplayName =
        "Accepting the invitation sets the password and verifies the email; " +
        "after approval the contact can sign in")]
    public async Task Accept_ThenApprove_AllowsLogin()
    {
        var contactId = await InviteAsAdminAsync();
        var token = Emails.LatestToken(ContactEmail, "accept-invitation");

        var accepted = await AcceptAsync(token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var data = await DataAsync(accepted);
        Assert.False(data["canSignIn"]!.GetValue<bool>());

        await using (var db = await CreateScopeDbContextAsync())
        {
            var user = await db.Users.SingleAsync(x => x.Id == contactId);
            Assert.True(user.EmailVerified);
            Assert.True(await db.AuditLogs.AnyAsync(
                x => x.Action == "InvitationAccepted" && x.EntityId == contactId));
        }

        // Still gated on approval.
        var beforeApproval = await LoginAttemptAsync(ContactEmail, ContactPassword);
        Assert.Equal(HttpStatusCode.Forbidden, beforeApproval.StatusCode);

        await ApproveAsync(contactId);

        var login = await LoginAsync(ContactEmail, ContactPassword);
        Assert.Equal("Client", login.User.Role);
        Assert.Equal(TestDataSeeder.Ids.CompanyA, login.User.ClientCompanyId);
    }

    [Fact(DisplayName = "An invitation link works only once")]
    public async Task Accept_IsSingleUse()
    {
        await InviteAsAdminAsync();
        var token = Emails.LatestToken(ContactEmail, "accept-invitation");

        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(token)).StatusCode);

        var second = await AcceptAsync(token, "Someone-Else-Pw1");
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact(DisplayName = "An expired invitation link is rejected")]
    public async Task Accept_Expired_IsRejected()
    {
        var contactId = await InviteAsAdminAsync();
        var token = Emails.LatestToken(ContactEmail, "accept-invitation");

        await using (var db = await CreateScopeDbContextAsync())
        {
            await db.PasswordResetTokens
                .Where(x => x.UserId == contactId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        var response = await AcceptAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var check = await CreateScopeDbContextAsync();
        Assert.False((await check.Users.SingleAsync(x => x.Id == contactId)).EmailVerified);
    }

    [Theory(DisplayName = "Unknown or malformed invitation tokens are rejected")]
    [InlineData("not-a-real-token")]
    [InlineData("")]
    public async Task Accept_UnknownToken_IsRejected(string token)
    {
        var response = await AcceptAsync(token);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
            $"Unexpected status {response.StatusCode}");
    }

    [Fact(DisplayName = "An invitation link cannot be used as a password reset link")]
    public async Task InvitationToken_DoesNotWorkForReset()
    {
        var contactId = await InviteAsAdminAsync();
        var token = Emails.LatestToken(ContactEmail, "accept-invitation");

        var reset = await PostAnonymousAsync(
            "/api/v1/auth/reset-password",
            new { token, newPassword = ContactPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, reset.StatusCode);

        // And the invitation itself still works afterwards.
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(token)).StatusCode);

        await using var db = await CreateScopeDbContextAsync();
        Assert.True((await db.Users.SingleAsync(x => x.Id == contactId)).EmailVerified);
    }

    [Fact(DisplayName = "Resending an invitation invalidates the previous link")]
    public async Task Resend_InvalidatesOldLink()
    {
        var contactId = await InviteAsAdminAsync();
        var firstToken = Emails.LatestToken(ContactEmail, "accept-invitation");

        var admin = await LoginAsync(AdminEmail, Password);
        var resent = await SendAsync(
            HttpMethod.Post,
            $"{CompanyAContacts}/{contactId}/resend-invitation",
            admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);

        var secondToken = Emails.LatestToken(ContactEmail, "accept-invitation");
        Assert.NotEqual(firstToken, secondToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await AcceptAsync(firstToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(secondToken)).StatusCode);
    }

    [Fact(DisplayName = "An invitation cannot be resent after it was accepted")]
    public async Task Resend_AfterAcceptance_IsConflict()
    {
        var contactId = await InviteAsAdminAsync();
        await AcceptAsync(Emails.LatestToken(ContactEmail, "accept-invitation"));

        var admin = await LoginAsync(AdminEmail, Password);
        var resent = await SendAsync(
            HttpMethod.Post,
            $"{CompanyAContacts}/{contactId}/resend-invitation",
            admin.AccessToken);

        Assert.Equal(HttpStatusCode.Conflict, resent.StatusCode);
    }

    [Fact(DisplayName =
        "A failed email delivery keeps the contact and reports invitationSent = false")]
    public async Task Invite_EmailFailure_IsReported()
    {
        Emails.FailSends = true;
        var admin = await LoginAsync(AdminEmail, Password);

        var response = await SendAsync(
            HttpMethod.Post,
            CompanyAContacts,
            admin.AccessToken,
            new { fullName = "Unlucky Contact", email = ContactEmail });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await DataAsync(response))["invitationSent"]!.GetValue<bool>());

        await using var db = await CreateScopeDbContextAsync();
        Assert.True(await db.Users.AnyAsync(x => x.Email == ContactEmail));
    }

    [Fact(DisplayName = "An Agent can invite a contact for a client assigned to them")]
    public async Task Agent_CanInvite_ForAssignedClient()
    {
        var agent = await LoginAsync(AgentAEmail, Password);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/clients/{TestDataSeeder.Ids.CompanyA}/contacts",
            agent.AccessToken,
            new { fullName = "Agent Invited", email = ContactEmail });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(Emails.LatestToken(ContactEmail, "accept-invitation"));

        await using var db = await CreateScopeDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Email == ContactEmail);
        Assert.Equal(TestDataSeeder.Ids.CompanyA, user.ClientCompanyId);
        Assert.False(user.IsApproved);
    }

    [Fact(DisplayName = "An Agent cannot invite contacts for a client outside their scope")]
    public async Task Agent_CannotInvite_ForUnassignedClient()
    {
        var agent = await LoginAsync(AgentAEmail, Password);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/agent/clients/{TestDataSeeder.Ids.CompanyB}/contacts",
            agent.AccessToken,
            new { fullName = "Out Of Scope", email = ContactEmail });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Emails.To(ContactEmail));

        await using var db = await CreateScopeDbContextAsync();
        Assert.False(await db.Users.AnyAsync(x => x.Email == ContactEmail));
    }

    [Fact(DisplayName = "Only the hash of an invitation token is stored")]
    public async Task OnlyTokenHashIsStored()
    {
        var contactId = await InviteAsAdminAsync();
        var token = Emails.LatestToken(ContactEmail, "accept-invitation")!;

        await using var db = await CreateScopeDbContextAsync();
        var stored = await db.PasswordResetTokens.SingleAsync(x => x.UserId == contactId);

        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(AccountTokens.Hash(token), stored.TokenHash);
    }
}
