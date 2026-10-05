using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Clients.Commands.ApproveClient;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Admin;

/// <summary>
/// Exercises the client approval workflow
/// (PATCH /api/v1/admin/clients/{clientId}/contacts/{contactId}/approval):
/// approve/reject state transitions, audit logs, persisted + realtime
/// notifications, account-state gates, authorization and idempotency.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientApprovalTests : IntegrationTestBase
{
    private Guid AdminId => TestDataSeeder.Ids.Admin;
    private Guid AgentA => TestDataSeeder.Ids.AgentA;
    private Guid ClientUserA => TestDataSeeder.Ids.ClientUserA;
    private Guid ClientUserB => TestDataSeeder.Ids.ClientUserB;
    private Guid CompanyA => TestDataSeeder.Ids.CompanyA;
    private Guid CompanyB => TestDataSeeder.Ids.CompanyB;

    public ClientApprovalTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static string ApprovalUrl(Guid clientId, Guid contactId)
        => $"/api/v1/admin/clients/{clientId}/contacts/{contactId}/approval";

    private Task<HttpResponseMessage> PatchApprovalAsync(
        Guid clientId,
        Guid contactId,
        object body,
        Guid userId,
        string role,
        string? clientCompanyId = null)
        => ScopeClient.SendAsync(
            ScopePatch(
                ApprovalUrl(clientId, contactId),
                body,
                userId,
                role,
                clientCompanyId));

    private Task<HttpResponseMessage> ApproveAsync(
        Guid clientId,
        Guid contactId)
        => PatchApprovalAsync(
            clientId, contactId, new { approved = true },
            AdminId, "Admin");

    private Task<HttpResponseMessage> RejectAsync(
        Guid clientId,
        Guid contactId)
        => PatchApprovalAsync(
            clientId, contactId, new { approved = false },
            AdminId, "Admin");

    private Task<HttpResponseMessage> LoginAsync(
        string email,
        string password)
        => AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

    private async Task<int> UnreadCountAsync(Guid userId)
    {
        var response = await ScopeClient.SendAsync(
            ScopeGet(
                "/api/v1/client/notifications/unread-count",
                userId,
                "Client"));

        await EnsureOk(response, HttpStatusCode.OK);

        var body = JsonNode.Parse(
            await response.Content.ReadAsStringAsync())!;

        return body["data"]!.GetValue<int>();
    }

    private async Task<(bool IsActive, bool IsApproved, bool EmailVerified)>
        ReadUserStateAsync(Guid userId)
    {
        (bool IsActive, bool IsApproved, bool EmailVerified)? state = null;

        await WithScopeDbContextAsync(db =>
        {
            var user = db.Users.First(x => x.Id == userId);
            state = (
                user.IsActive,
                user.IsApproved,
                user.EmailVerified);
        });

        return state!.Value;
    }

    private async Task<int> CountAuditLogsAsync(
        string action,
        Guid entityId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.AuditLogs.Count(x =>
                x.Action == action &&
                x.EntityId == entityId);
        });

        return count;
    }

    private async Task<int> CountNotificationsAsync(Guid userId)
    {
        var count = 0;

        await WithScopeDbContextAsync(db =>
        {
            count = db.Notifications.Count(x =>
                x.UserId == userId);
        });

        return count;
    }

    private Task MakeClientPendingAsync(
        Guid userId,
        bool emailVerified = true)
        => WithScopeDbContextAsync(db =>
        {
            var user = db.Users.First(x => x.Id == userId);
            user.IsApproved = false;
            user.EmailVerified = emailVerified;
            user.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

    private async Task<Guid> SeedUserAsync(
        UserRole role,
        Guid? clientCompanyId,
        bool isApproved = false,
        bool isActive = true)
    {
        var id = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.Users.Add(new User
            {
                Id = id,
                FullName = "Scoped User",
                Email = $"scoped-{Guid.NewGuid():N}@test.local",
                PasswordHash = "not-used",
                Role = role,
                IsActive = isActive,
                IsApproved = isApproved,
                EmailVerified = true,
                ClientCompanyId = clientCompanyId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            });
            db.SaveChanges();
        });

        return id;
    }

    // ------------------------------------------------------------------
    // Approve
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin can approve a pending client contact and the account becomes usable")]
    public async Task ApprovePendingClient_MarksApprovedAndActive_KeepsEmailVerification()
    {
        await MakeClientPendingAsync(ClientUserA, emailVerified: false);

        var response = await ApproveAsync(CompanyA, ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadJsonAsync<ApiResponse<ApproveClientResponseDto>>(
            response);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(ClientUserA, envelope.Data?.Id);
        Assert.True(envelope.Data?.IsApproved);
        Assert.True(envelope.Data?.IsActive);
        Assert.False(envelope.Data?.EmailVerified);

        var state = await ReadUserStateAsync(ClientUserA);

        Assert.True(state.IsApproved);
        Assert.True(state.IsActive);
        Assert.False(state.EmailVerified);

        Assert.Equal(1,
            await CountAuditLogsAsync("ClientApproved", ClientUserA));
        Assert.Equal(1, await CountNotificationsAsync(ClientUserA));
        Assert.Equal(1, await UnreadCountAsync(ClientUserA));
    }

    [Fact(DisplayName =
        "Approved but email-unverified client still cannot log in (403)")]
    public async Task ApprovedUnverifiedClient_LoginReturns403()
    {
        await MakeClientPendingAsync(ClientUserA, emailVerified: false);

        await ApproveAsync(CompanyA, ClientUserA);

        var login = await LoginAsync(
            "client-a@test.local",
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    // ------------------------------------------------------------------
    // Reject
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin can reject a pending client contact and login stays blocked")]
    public async Task RejectPendingClient_DeactivatesAndKeepsUnapproved()
    {
        await MakeClientPendingAsync(ClientUserA);

        var response = await RejectAsync(CompanyA, ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);

        var state = await ReadUserStateAsync(ClientUserA);

        Assert.False(state.IsApproved);
        Assert.False(state.IsActive);

        Assert.Equal(1,
            await CountAuditLogsAsync("ClientRejected", ClientUserA));
        Assert.Equal(1, await CountNotificationsAsync(ClientUserA));

        var login = await LoginAsync(
            "client-a@test.local",
            TestDataSeeder.TestLoginPassword);

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    // ------------------------------------------------------------------
    // Guards: 404 semantics (missing / out-of-scope / wrong role / deleted)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Client without a client company is not approved (404), no mutation")]
    public async Task ApproveContactWithoutCompany_Returns404_AndDoesNotApprove()
    {
        var noCompanyUser = await SeedUserAsync(
            UserRole.Client, clientCompanyId: null);

        var response = await ApproveAsync(CompanyA, noCompanyUser);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var state = await ReadUserStateAsync(noCompanyUser);

        Assert.False(state.IsApproved);
        Assert.True(state.IsActive);
        Assert.Equal(0,
            await CountAuditLogsAsync("ClientApproved", noCompanyUser));
        Assert.Equal(0, await CountNotificationsAsync(noCompanyUser));
    }

    [Fact(DisplayName =
        "Rejecting a client without a client company returns 404")]
    public async Task RejectContactWithoutCompany_Returns404()
    {
        var noCompanyUser = await SeedUserAsync(
            UserRole.Client, clientCompanyId: null);

        var response = await RejectAsync(CompanyA, noCompanyUser);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval route rejects a contact belonging to another company (404)")]
    public async Task ApproveCrossCompanyContact_Returns404()
    {
        var response = await ApproveAsync(CompanyA, ClientUserB);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval route returns 404 for an unknown contact")]
    public async Task ApproveUnknownContact_Returns404()
    {
        var response = await ApproveAsync(CompanyA, TestDataSeeder.Ids.Unknown);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval route returns 404 when the target is not a Client role")]
    public async Task ApproveNonClientRole_Returns404()
    {
        var agentWithCompany = await SeedUserAsync(
            UserRole.Agent, clientCompanyId: CompanyA);

        var response = await ApproveAsync(CompanyA, agentWithCompany);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval route returns 404 for a soft-deleted contact")]
    public async Task ApproveDeletedContact_Returns404()
    {
        await WithScopeDbContextAsync(db =>
        {
            var user = db.Users.First(x => x.Id == ClientUserA);
            user.IsDeleted = true;
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        });

        var response = await ApproveAsync(CompanyA, ClientUserA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Authorization
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Approval route without a token returns 401")]
    public async Task NoToken_Returns401()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            ApprovalUrl(CompanyA, ClientUserA));

        request.Content = JsonContent.Create(new { approved = true });

        var response = await AuthClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval route with an agent token returns 403")]
    public async Task AgentToken_Returns403()
    {
        var response = await PatchApprovalAsync(
            CompanyA, ClientUserA, new { approved = true },
            AgentA, "Agent");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval route with a client token returns 403")]
    public async Task ClientToken_Returns403()
    {
        var response = await PatchApprovalAsync(
            CompanyA, ClientUserA, new { approved = true },
            ClientUserA, "Client", CompanyA.ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Approval request without the 'approved' field returns 400")]
    public async Task MissingApprovedField_Returns400()
    {
        var response = await PatchApprovalAsync(
            CompanyA, ClientUserA, new { },
            AdminId, "Admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Idempotency
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Approving an already approved client is a no-op (no duplicate audit/notification)")]
    public async Task ApproveAlreadyApproved_NoDuplicateAuditOrNotification()
    {
        await ApproveAsync(CompanyA, ClientUserA);
        await ApproveAsync(CompanyA, ClientUserA);

        var state = await ReadUserStateAsync(ClientUserA);

        Assert.True(state.IsApproved);
        Assert.True(state.IsActive);
        Assert.Equal(0,
            await CountAuditLogsAsync("ClientApproved", ClientUserA));
        Assert.Equal(0, await CountNotificationsAsync(ClientUserA));
    }

    [Fact(DisplayName =
        "Rejecting an already rejected client is a no-op (no duplicate audit/notification)")]
    public async Task RejectAlreadyRejected_NoDuplicateAuditOrNotification()
    {
        await MakeClientPendingAsync(ClientUserA);

        await RejectAsync(CompanyA, ClientUserA);

        var state = await ReadUserStateAsync(ClientUserA);

        Assert.False(state.IsApproved);
        Assert.False(state.IsActive);
        Assert.Equal(1,
            await CountAuditLogsAsync("ClientRejected", ClientUserA));
        Assert.Equal(1, await CountNotificationsAsync(ClientUserA));

        await RejectAsync(CompanyA, ClientUserA);

        Assert.Equal(1,
            await CountAuditLogsAsync("ClientRejected", ClientUserA));
        Assert.Equal(1, await CountNotificationsAsync(ClientUserA));
    }

    [Fact(DisplayName =
        "Rejecting an approved client revokes approval and deactivates the account")]
    public async Task RejectApprovedClient_RevokesApproval()
    {
        var response = await RejectAsync(CompanyA, ClientUserA);

        await EnsureOk(response, HttpStatusCode.OK);

        var state = await ReadUserStateAsync(ClientUserA);

        Assert.False(state.IsApproved);
        Assert.False(state.IsActive);

        Assert.Equal(1,
            await CountAuditLogsAsync("ClientRejected", ClientUserA));
        Assert.Equal(1, await CountNotificationsAsync(ClientUserA));
    }
}