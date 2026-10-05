using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Clients.Commands.CreateClientContact;
using PROManagementSystem.Application.Staff.Commands.CreateStaff;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Regression cover for account acquisition through the real HTTP login
/// endpoint. These accounts are created by the Admin endpoints and are
/// therefore never exercised by the TestIdentity scheme used elsewhere:
/// before this coverage existed, an Admin-provisioned account could be
/// created successfully yet be permanently unable to obtain a session.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AdminProvisionedAccountLoginTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    private const string StaffPassword = "ProvisionedStaffPw1!";

    // The Admin chooses the contact password, so the test already holds it.
    // Nothing is returned at creation and nothing has to be scraped out of a
    // response to attempt the login.
    private const string ContactPassword = "ProvisionedContactPw1!";

    private const string NewStaffEmail =
        "provisioned-staff@test.local";

    private const string NewContactEmail =
        "provisioned-contact@test.local";

    public AdminProvisionedAccountLoginTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = AdminEmail,
                password =
                    TestDataSeeder.TestLoginPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        Assert.False(
            string.IsNullOrWhiteSpace(
                envelope.Data.AccessToken));

        return envelope.Data.AccessToken;
    }

    private Task<HttpResponseMessage> LoginAttemptAsync(
        string email,
        string password)
        => AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

    private static HttpRequestMessage Bearer(
        HttpMethod method,
        string url,
        object? body,
        string token)
    {
        var request =
            new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content =
                System.Net.Http.Json.JsonContent.Create(body);
        }

        return request;
    }

    private async Task<CreateStaffResponseDto> CreateStaffAsync(
        string adminToken,
        string email,
        string password)
    {
        var response = await AuthClient.SendAsync(Bearer(
            HttpMethod.Post,
            "/api/v1/admin/staff",
            new
            {
                fullName = "Provisioned Staff",
                email,
                password,
                phone = "+910000000001"
            },
            adminToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<CreateStaffResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    private async Task<CreateClientContactResponseDto>
        CreateContactAsync(
        string adminToken,
        string email,
        string password)
    {
        var response = await AuthClient.SendAsync(Bearer(
            HttpMethod.Post,
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}" +
            "/contacts",
            new
            {
                fullName = "Provisioned Contact",
                email,
                phone = "+910000000002",
                password
            },
            adminToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<CreateClientContactResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    private async Task ApproveContactAsync(
        string adminToken,
        Guid contactId)
    {
        var response = await AuthClient.SendAsync(Bearer(
            HttpMethod.Patch,
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}" +
            $"/contacts/{contactId}/approval",
            new { approved = true },
            adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName =
        "Staff created by an Admin can log in with the Admin-set password")]
    public async Task Staff_CreatedByAdmin_CanLogIn()
    {
        var adminToken = await LoginAsAdminAsync();

        await CreateStaffAsync(
            adminToken,
            NewStaffEmail,
            StaffPassword);

        var response = await LoginAttemptAsync(
            NewStaffEmail,
            StaffPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope?.Data);
        Assert.False(
            string.IsNullOrWhiteSpace(
                envelope!.Data.AccessToken));
    }

    [Theory(DisplayName =
        "Admin-created staff can log in with the stored lowercase email")]
    [InlineData("exact-staff-email@test.local")]
    [InlineData("EXACT-STAFF-EMAIL@TEST.LOCAL")]
    [InlineData("Exact-Staff-Email@Test.Local")]
    [InlineData("  exact-staff-email@test.local  ")]
    [InlineData("\texact-staff-email@test.local\n")]
    [InlineData("  EXACT-STAFF-EMAIL@TEST.LOCAL  ")]
    public async Task Staff_CreatedByAdmin_CanLogIn_WithEmailFormattingVariants(
        string submittedEmail)
    {
        // CreateStaffCommandHandler stores
        // request.Email.Trim().ToLowerInvariant(), so the column holds a
        // normalized value. LoginCommandHandler used to compare the RAW
        // request against it, which turned a correct password into
        // 401 "Invalid email or password" purely because the user typed a
        // different case or left whitespace on the field. Every other entry
        // point (Register, ForgotPassword) already normalized.
        var adminToken = await LoginAsAdminAsync();

        await CreateStaffAsync(
            adminToken,
            "exact-staff-email@test.local",
            StaffPassword);

        var response = await LoginAttemptAsync(
            submittedEmail,
            StaffPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope?.Data);
        Assert.False(
            string.IsNullOrWhiteSpace(
                envelope!.Data.AccessToken));
    }

    [Fact(DisplayName =
        "Normalizing the login email did not weaken it: an unknown " +
        "address is still rejected")]
    public async Task StaffLogin_UnknownEmail_StillRejected()
    {
        var adminToken = await LoginAsAdminAsync();

        await CreateStaffAsync(
            adminToken,
            "known-staff-email@test.local",
            StaffPassword);

        var response = await LoginAttemptAsync(
            "  NOBODY-AT-ALL@TEST.LOCAL  ",
            StaffPassword);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "Normalizing the login email did not weaken it: a wrong password " +
        "is still rejected for a correctly formatted address")]
    public async Task StaffLogin_WrongPassword_StillRejected()
    {
        var adminToken = await LoginAsAdminAsync();

        await CreateStaffAsync(
            adminToken,
            "guarded-staff-email@test.local",
            StaffPassword);

        // Mixed case and padding on purpose: the formatting is now tolerated,
        // so the only thing left to fail on is the password.
        var response = await LoginAttemptAsync(
            "  GUARDED-STAFF-EMAIL@TEST.LOCAL  ",
            "TheWrongPassword1!");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "Client contact created by an Admin cannot log in before approval")]
    public async Task ClientContact_BeforeApproval_CannotLogIn()
    {
        var adminToken = await LoginAsAdminAsync();

        var contact = await CreateContactAsync(
            adminToken,
            NewContactEmail,
            ContactPassword);

        var response = await LoginAttemptAsync(
            NewContactEmail,
            ContactPassword);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "Client contact can log in after approval using the password the " +
        "Admin chose at creation")]
    public async Task ClientContact_AfterApproval_CanLogIn()
    {
        var adminToken = await LoginAsAdminAsync();

        var contact = await CreateContactAsync(
            adminToken,
            NewContactEmail,
            ContactPassword);

        await ApproveContactAsync(
            adminToken,
            contact.Id);

        var response = await LoginAttemptAsync(
            NewContactEmail,
            ContactPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope?.Data);
        Assert.False(
            string.IsNullOrWhiteSpace(
                envelope!.Data.AccessToken));
    }

    [Fact(DisplayName =
        "Admin-provisioned accounts are created email-verified while the " +
        "approval gate is preserved")]
    public async Task CreatedAccounts_AreEmailVerified()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(
            adminToken,
            NewStaffEmail,
            StaffPassword);

        var contact = await CreateContactAsync(
            adminToken,
            NewContactEmail,
            ContactPassword);

        await WithScopeDbContextAsync(db =>
        {
            var staffRow =
                db.Users.Single(x => x.Id == staff.Id);

            Assert.True(staffRow.IsActive);
            Assert.True(staffRow.IsApproved);
            Assert.True(staffRow.EmailVerified);

            var contactRow =
                db.Users.Single(x => x.Id == contact.Id);

            Assert.True(contactRow.IsActive);
            Assert.False(contactRow.IsApproved);
            Assert.True(contactRow.EmailVerified);
        });
    }

    [Fact(DisplayName =
        "The email-verified gate still blocks login when the flag is " +
        "cleared, so provisioning does not disable the control")]
    public async Task EmailVerifiedGate_StillBlocksLogin_WhenCleared()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(
            adminToken,
            NewStaffEmail,
            StaffPassword);

        await WithScopeDbContextAsync(db =>
        {
            var row = db.Users.Single(x => x.Id == staff.Id);
            row.EmailVerified = false;
            db.SaveChanges();
        });

        var response = await LoginAttemptAsync(
            NewStaffEmail,
            StaffPassword);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "The approval gate still blocks login when the flag is cleared, " +
        "so normalizing the email does not skip it")]
    public async Task IsApprovedGate_StillBlocksLogin_WhenCleared()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(
            adminToken,
            "gate-staff-approved@test.local",
            StaffPassword);

        await WithScopeDbContextAsync(db =>
        {
            var row = db.Users.Single(x => x.Id == staff.Id);
            row.IsApproved = false;
            db.SaveChanges();
        });

        // Mixed case on purpose: the address normalizes, so the 403 has to
        // come from the approval gate rather than from a failed lookup.
        var response = await LoginAttemptAsync(
            "GATE-STAFF-APPROVED@TEST.LOCAL",
            StaffPassword);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "The active gate still blocks login when the account is " +
        "deactivated, so normalizing the email does not skip it")]
    public async Task IsActiveGate_StillBlocksLogin_WhenCleared()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(
            adminToken,
            "gate-staff-active@test.local",
            StaffPassword);

        await WithScopeDbContextAsync(db =>
        {
            var row = db.Users.Single(x => x.Id == staff.Id);
            row.IsActive = false;
            db.SaveChanges();
        });

        var response = await LoginAttemptAsync(
            "  GATE-STAFF-ACTIVE@TEST.LOCAL  ",
            StaffPassword);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
}
