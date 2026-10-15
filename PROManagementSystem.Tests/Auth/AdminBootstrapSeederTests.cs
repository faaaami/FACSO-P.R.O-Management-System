using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Api.Common.Middlewares;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Identity;
using PROManagementSystem.Infrastructure.Persistence.Seeders;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// First-Admin bootstrap from ADMIN_EMAIL / ADMIN_PASSWORD, and the forced
/// password change that follows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminBootstrapSeederTests : AuthFlowTestBase
{
    private const string BootstrapEmail = "first-admin@fasco.test";
    private const string BootstrapPassword = "Temporary-Admin-Pw-2026";
    private const string NewPassword = "My-Own-Admin-Pw-2026";

    private static readonly Guid AutomationUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    public AdminBootstrapSeederTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task RemoveSeededAdminsAsync()
    {
        await using var db = await CreateScopeDbContextAsync();

        await db.Users
            .Where(x => x.Role == UserRole.Admin)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.DeletedAt, DateTime.UtcNow));
    }

    private async Task<AdminBootstrapResult> SeedAsync(
        string email = BootstrapEmail,
        string password = BootstrapPassword)
    {
        await using var db = await CreateScopeDbContextAsync();

        return AdminBootstrapSeeder.Seed(
            db,
            new BCryptPasswordHasher(),
            email,
            password,
            AutomationUserId);
    }

    private async Task AddAutomationAdminAsync()
    {
        await using var db = await CreateScopeDbContextAsync();
        var now = DateTime.UtcNow;

        db.Users.Add(new User
        {
            Id = AutomationUserId,
            FullName = "Renewal Automation",
            Email = "automation@internal.local",
            PasswordHash = "not-a-real-hash",
            Role = UserRole.Admin,
            IsActive = true,
            IsApproved = true,
            EmailVerified = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync();
    }

    [Fact(DisplayName =
        "Bootstrap creates an approved, verified Admin that must change its password")]
    public async Task NoAdmin_CreatesAdminWithForcedPasswordChange()
    {
        await RemoveSeededAdminsAsync();

        Assert.Equal(AdminBootstrapResult.Created, await SeedAsync());

        await using var db = await CreateScopeDbContextAsync();
        var admin = await db.Users.SingleAsync(x => x.Email == BootstrapEmail);

        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.IsActive);
        Assert.True(admin.IsApproved);
        Assert.True(admin.EmailVerified);
        Assert.True(admin.MustChangePassword);
        Assert.NotEqual(BootstrapPassword, admin.PasswordHash);
        Assert.True(await db.AuditLogs.AnyAsync(
            x => x.Action == "AdminBootstrapped" && x.EntityId == admin.Id));
    }

    [Fact(DisplayName = "Bootstrap does nothing when an Admin already exists")]
    public async Task AdminExists_Skips()
    {
        Assert.Equal(AdminBootstrapResult.SkippedAdminExists, await SeedAsync());

        await using var db = await CreateScopeDbContextAsync();
        Assert.False(await db.Users.AnyAsync(x => x.Email == BootstrapEmail));
    }

    [Fact(DisplayName =
        "The automation system account does not count as an existing Admin")]
    public async Task OnlyAutomationAdmin_StillCreates()
    {
        await RemoveSeededAdminsAsync();
        await AddAutomationAdminAsync();

        Assert.Equal(AdminBootstrapResult.Created, await SeedAsync());
    }

    [Fact(DisplayName = "Bootstrap is idempotent across restarts")]
    public async Task SecondRun_Skips()
    {
        await RemoveSeededAdminsAsync();

        Assert.Equal(AdminBootstrapResult.Created, await SeedAsync());
        Assert.Equal(AdminBootstrapResult.SkippedAdminExists, await SeedAsync());

        await using var db = await CreateScopeDbContextAsync();
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == BootstrapEmail));
    }

    [Fact(DisplayName = "Bootstrap never takes over an existing account's email")]
    public async Task EmailInUse_Skips()
    {
        await RemoveSeededAdminsAsync();

        Assert.Equal(
            AdminBootstrapResult.SkippedEmailInUse,
            await SeedAsync(email: AgentAEmail));

        await using var db = await CreateScopeDbContextAsync();
        var agent = await db.Users.SingleAsync(x => x.Email == AgentAEmail);
        Assert.Equal(UserRole.Agent, agent.Role);
    }

    [Theory(DisplayName = "Bootstrap rejects invalid input")]
    [InlineData("not-an-email", BootstrapPassword, AdminBootstrapResult.InvalidEmail)]
    [InlineData(BootstrapEmail, "short-pw", AdminBootstrapResult.PasswordTooShort)]
    public async Task InvalidInput_IsRejected(
        string email,
        string password,
        AdminBootstrapResult expected)
    {
        await RemoveSeededAdminsAsync();

        Assert.Equal(expected, await SeedAsync(email, password));
    }

    [Fact(DisplayName =
        "A bootstrapped Admin is blocked from everything but changing the password, " +
        "then has full access after changing it")]
    public async Task BootstrappedAdmin_MustChangePasswordBeforeUsingTheApi()
    {
        await RemoveSeededAdminsAsync();
        Assert.Equal(AdminBootstrapResult.Created, await SeedAsync());

        var login = await LoginAsync(BootstrapEmail, BootstrapPassword);
        Assert.True(login.User.MustChangePassword);

        // Everything except the password change is refused.
        var blocked = await SendAsync(
            HttpMethod.Get, "/api/v1/admin/clients", login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(
            PasswordChangeRequiredMiddleware.ErrorCode,
            await ErrorCodeAsync(blocked));

        // Reading one's own profile is allowed.
        var profile = await SendAsync(
            HttpMethod.Get, "/api/v1/users/me", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        var changed = await SendAsync(
            HttpMethod.Post,
            "/api/v1/users/change-password",
            login.AccessToken,
            new
            {
                currentPassword = BootstrapPassword,
                newPassword = NewPassword,
                confirmPassword = NewPassword
            });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // A refreshed token no longer carries the restriction.
        var refreshed = await PostAnonymousAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var data = await DataAsync(refreshed);
        Assert.False(data["mustChangePassword"]!.GetValue<bool>());
        var accessToken = data["accessToken"]!.GetValue<string>();

        var allowed = await SendAsync(
            HttpMethod.Get, "/api/v1/admin/clients", accessToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        // The flag is cleared for future sign-ins, and the old password is gone.
        var relogin = await LoginAsync(BootstrapEmail, NewPassword);
        Assert.False(relogin.User.MustChangePassword);

        var oldPassword = await LoginAttemptAsync(BootstrapEmail, BootstrapPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
    }
}
