using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Staff.Commands.CreateStaff;
using PROManagementSystem.Application.Staff.Commands.UpdateStaff;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Staff;

/// <summary>
/// PATCH /api/v1/admin/staff/{id} is a partial-update verb, so a field the
/// caller omits must not be written. This cover exists because the request DTO
/// used non-nullable scalars that the handler assigned unconditionally: a
/// payload that omitted <c>isApproved</c> therefore wrote <c>false</c> and
/// silently revoked login eligibility (LoginCommandHandler refuses an
/// unapproved account), and a payload that omitted <c>email</c> dereferenced
/// null and returned 500.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class UpdateStaffPartialPayloadTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    private const string StaffPassword = "PartialPatchStaffPw1!";

    private const string StaffEmail = "partial-patch-staff@test.local";

    public UpdateStaffPartialPayloadTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Happy-path scaffolding
    // ------------------------------------------------------------------

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

        Assert.NotNull(envelope?.Data);

        return envelope!.Data.AccessToken;
    }

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
        string email)
    {
        var response = await AuthClient.SendAsync(Bearer(
            HttpMethod.Post,
            "/api/v1/admin/staff",
            new
            {
                fullName = "Partial Patch Subject",
                email,
                password = StaffPassword,
                phone = "+910000000009"
            },
            adminToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<CreateStaffResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    private async Task<UpdateStaffResponseDto> PatchAsync(
        string adminToken,
        Guid staffId,
        object body)
    {
        var response = await AuthClient.SendAsync(Bearer(
            HttpMethod.Patch,
            $"/api/v1/admin/staff/{staffId}",
            body,
            adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<UpdateStaffResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    private Task<HttpResponseMessage> LoginAttemptAsync(
        string email,
        string password)
        => AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

    // ------------------------------------------------------------------
    // Criteria
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "PATCH omitting isApproved preserves the existing approval state")]
    public async Task OmittedIsApproved_PreservesApproval()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(adminToken, StaffEmail);

        // Admin-created staff start approved (CreateStaffCommandHandler).
        Assert.True(staff.IsApproved);

        var updated = await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Renamed Without Approval Field",
                email = StaffEmail,
                phone = "+910000000009"
            });

        Assert.True(updated.IsApproved);

        // The applied name proves the payload was really processed and the
        // request was not a silent no-op.
        Assert.Equal(
            "Renamed Without Approval Field",
            updated.FullName);
    }

    [Fact(DisplayName =
        "PATCH omitting email preserves the existing email")]
    public async Task OmittedEmail_PreservesEmail()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(adminToken, StaffEmail);

        var updated = await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Renamed Without Email Field",
                phone = "+910000000009"
            });

        Assert.Equal(StaffEmail, updated.Email);
    }

    [Fact(DisplayName =
        "PATCH with an explicit isApproved=false still revokes approval")]
    public async Task ExplicitIsApprovedFalse_RevokesApproval()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(adminToken, StaffEmail);

        Assert.True(staff.IsApproved);

        var updated = await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Explicitly Revoked",
                email = StaffEmail,
                phone = "+910000000009",
                isApproved = false
            });

        Assert.False(updated.IsApproved);

        var response = await LoginAttemptAsync(
            StaffEmail,
            StaffPassword);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "PATCH with an explicit isApproved=true still approves")]
    public async Task ExplicitIsApprovedTrue_Approves()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(adminToken, StaffEmail);

        // Start from a revoked state so the assertion proves the write
        // happened rather than merely echoing the seeded value.
        var revoked = await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Temporarily Revoked",
                email = StaffEmail,
                phone = "+910000000009",
                isApproved = false
            });

        Assert.False(revoked.IsApproved);

        var approved = await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Explicitly Approved",
                email = StaffEmail,
                phone = "+910000000009",
                isApproved = true
            });

        Assert.True(approved.IsApproved);
    }

    [Fact(DisplayName =
        "Staff stay login-capable after a PATCH that omits isApproved")]
    public async Task OmittedIsApproved_AccountRemainsLoginCapable()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(adminToken, StaffEmail);

        await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Renamed Then Logged In",
                email = StaffEmail,
                phone = "+910000000009"
            });

        var response = await LoginAttemptAsync(
            StaffEmail,
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
        "PATCH with a full payload still writes every supplied field")]
    public async Task FullPayload_WritesEverySuppliedField()
    {
        var adminToken = await LoginAsAdminAsync();

        var staff = await CreateStaffAsync(adminToken, StaffEmail);

        var updated = await PatchAsync(
            adminToken,
            staff.Id,
            new
            {
                fullName = "Fully Specified Subject",
                email = "renamed-staff@test.local",
                phone = "+910000000010",
                isApproved = true
            });

        Assert.Equal("Fully Specified Subject", updated.FullName);
        Assert.Equal("renamed-staff@test.local", updated.Email);
        Assert.Equal("+910000000010", updated.Phone);
        Assert.True(updated.IsApproved);

        // The email change must be persisted in normalized form.
        Assert.Equal(
            "renamed-staff@test.local",
            updated.Email);
    }
}