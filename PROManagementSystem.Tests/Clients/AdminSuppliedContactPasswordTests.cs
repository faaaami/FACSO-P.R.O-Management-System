using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Clients.Commands.CreateClientContact;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Clients;

/// <summary>
/// The initial password for a client contact is chosen by the Admin and sent in
/// the create request. This replaced a generated credential that the server
/// returned in the create response and emailed.
///
/// <para>
/// These tests assert the handling of a value the Admin already holds, so there
/// is no randomness to sample and none is asserted. What is locked in is that
/// the value is accepted exactly as given, is stored only as a hash, and is
/// never reflected back to the caller.
/// </para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AdminSuppliedContactPasswordTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    private const string Password = "ChosenContactPw1!";

    public AdminSuppliedContactPasswordTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Scaffolding
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

    private HttpRequestMessage ContactCreate(
        string adminToken,
        string email,
        object? password,
        string fullName = "Credential Subject") =>
        Bearer(
            HttpMethod.Post,
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}" +
            "/contacts",
            new
            {
                fullName,
                email,
                phone = "+910000000031",
                password
            },
            adminToken);

    private async Task<CreateClientContactResponseDto>
        CreateContactAsync(
        string adminToken,
        string email,
        string password = Password)
    {
        var response = await AuthClient.SendAsync(
            ContactCreate(adminToken, email, password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<CreateClientContactResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    // ------------------------------------------------------------------
    // The supplied value is what gets stored
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The Admin-supplied password is hashed and verifies, and the " +
        "plaintext is never stored")]
    public async Task SuppliedPassword_IsHashedNotStored()
    {
        var adminToken = await LoginAsAdminAsync();

        const string chosen = "UniquelyTypedPw1!";

        var created = await CreateContactAsync(
            adminToken,
            "pw-hashed@test.local",
            chosen);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher>();

        var row = db.Users.Single(x => x.Id == created.Id);

        Assert.False(string.IsNullOrWhiteSpace(row.PasswordHash));

        // The stored column is a hash, not the value that was sent.
        Assert.NotEqual(chosen, row.PasswordHash);

        // And the hash still validates the exact value the Admin typed.
        Assert.True(hasher.Verify(chosen, row.PasswordHash));
    }

    [Fact(DisplayName =
        "Each contact keeps its own Admin-supplied password")]
    public async Task SuppliedPasswords_AreIndependentPerContact()
    {
        var adminToken = await LoginAsAdminAsync();

        const string first = "FirstContactPw1!";
        const string second = "SecondContactPw2@";

        var firstContact = await CreateContactAsync(
            adminToken,
            "pw-independent-a@test.local",
            first);

        var secondContact = await CreateContactAsync(
            adminToken,
            "pw-independent-b@test.local",
            second);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher>();

        var firstRow = db.Users.Single(
            x => x.Id == firstContact.Id);
        var secondRow = db.Users.Single(
            x => x.Id == secondContact.Id);

        Assert.True(hasher.Verify(first, firstRow.PasswordHash));
        Assert.True(hasher.Verify(second, secondRow.PasswordHash));

        // Cross-checks: neither value satisfies the other's hash.
        Assert.False(hasher.Verify(second, firstRow.PasswordHash));
        Assert.False(hasher.Verify(first, secondRow.PasswordHash));
    }

    [Fact(DisplayName =
        "The supplied password is the credential that works after " +
        "approval")]
    public async Task SuppliedPassword_AuthenticatesAfterApproval()
    {
        var adminToken = await LoginAsAdminAsync();

        const string email = "pw-authenticates@test.local";

        var created = await CreateContactAsync(
            adminToken,
            email,
            "AuthenticatesPw1!");

        var beforeApproval = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email,
                password = "AuthenticatesPw1!"
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            beforeApproval.StatusCode);

        var approve = await AuthClient.SendAsync(Bearer(
            HttpMethod.Patch,
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}" +
            $"/contacts/{created.Id}/approval",
            new { approved = true },
            adminToken));

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var afterApproval = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email,
                password = "AuthenticatesPw1!"
            });

        Assert.Equal(HttpStatusCode.OK, afterApproval.StatusCode);

        var envelope = await ReadJsonAsync<
            ApiResponse<LoginResponseDto>>(afterApproval);

        Assert.NotNull(envelope?.Data);
        Assert.False(string.IsNullOrWhiteSpace(
            envelope!.Data.AccessToken));
    }

    // ------------------------------------------------------------------
    // The response carries no credential
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The create response contains no password of any kind")]
    public async Task Response_ContainsNoPassword()
    {
        var adminToken = await LoginAsAdminAsync();

        var response = await AuthClient.SendAsync(
            ContactCreate(
                adminToken,
                "pw-no-echo@test.local",
                Password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Asserted against the raw wire payload rather than the typed DTO: a
        // field the contract no longer has could still be emitted by an
        // accidental anonymous projection, and only the JSON shows that.
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            "password",
            raw,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            Password,
            raw,
            StringComparison.Ordinal);

        using var document = JsonDocument.Parse(raw);
        var data = document.RootElement.GetProperty("data");

        Assert.False(data.TryGetProperty("password", out _));
        Assert.False(
            data.TryGetProperty("temporaryPassword", out _));
    }

    [Fact(DisplayName =
        "The audit trail records the creation without the password")]
    public async Task AuditLog_ContainsNoPassword()
    {
        var adminToken = await LoginAsAdminAsync();

        const string chosen = "AuditedSecretPw1!";

        var created = await CreateContactAsync(
            adminToken,
            "pw-audit@test.local",
            chosen);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var entries = db.AuditLogs
            .Where(x => x.Action == "ClientContactCreated")
            .ToList();

        Assert.NotEmpty(entries);

        var mine = entries.Single(
            x => x.EntityId == created.Id);

        Assert.DoesNotContain(
            chosen,
            mine.Description,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "password",
            mine.Description,
            StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A contact cannot be created without a password")]
    public async Task MissingPassword_IsRejected()
    {
        var adminToken = await LoginAsAdminAsync();

        var response = await AuthClient.SendAsync(
            ContactCreate(
                adminToken,
                "pw-missing@test.local",
                password: null));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory(DisplayName =
        "A contact cannot be created with a password outside 8 to 100 " +
        "characters")]
    [InlineData("Short1!")]
    [InlineData("")]
    public async Task OutOfLengthPassword_IsRejected(string password)
    {
        var adminToken = await LoginAsAdminAsync();

        var response = await AuthClient.SendAsync(
            ContactCreate(
                adminToken,
                $"pw-len-{password.Length}-{password.GetHashCode():X}@test.local",
                password));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact(DisplayName =
        "A 100-character password is accepted at the boundary")]
    public async Task MaximumLengthPassword_IsAccepted()
    {
        var adminToken = await LoginAsAdminAsync();

        var created = await CreateContactAsync(
            adminToken,
            "pw-maxlen@test.local",
            new string('a', 100));

        Assert.NotEqual(Guid.Empty, created.Id);
    }

    [Fact(DisplayName =
        "An 8-character password is accepted at the lower boundary")]
    public async Task MinimumLengthPassword_IsAccepted()
    {
        var adminToken = await LoginAsAdminAsync();

        var created = await CreateContactAsync(
            adminToken,
            "pw-minlen@test.local",
            new string('b', 8));

        Assert.NotEqual(Guid.Empty, created.Id);
    }

    // ------------------------------------------------------------------
    // Existing constraints still hold on this path
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The created account keeps the documented state: active, " +
        "unapproved, email-verified, Client role")]
    public async Task CreatedAccount_KeepsExpectedState()
    {
        var adminToken = await LoginAsAdminAsync();

        var created = await CreateContactAsync(
            adminToken,
            "pw-state@test.local");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var row = db.Users.Single(x => x.Id == created.Id);

        Assert.Equal("Client", row.Role.ToString());
        Assert.True(row.IsActive);
        Assert.False(row.IsApproved);
        Assert.True(row.EmailVerified);
        Assert.Equal(
            TestDataSeeder.Ids.CompanyA,
            row.ClientCompanyId);

        // The password path changed nothing about the response shape the
        // drawer renders.
        Assert.Equal("Client", created.Role);
        Assert.True(created.IsActive);
        Assert.False(created.IsApproved);
        Assert.Equal(
            TestDataSeeder.Ids.CompanyA,
            created.ClientCompanyId);
    }

    [Fact(DisplayName =
        "A duplicate contact email is still rejected once a password is " +
        "supplied")]
    public async Task DuplicateEmail_IsStillRejected()
    {
        var adminToken = await LoginAsAdminAsync();

        var first = await CreateContactAsync(
            adminToken,
            "pw-duplicate@test.local");

        var response = await AuthClient.SendAsync(
            ContactCreate(
                adminToken,
                "PW-Duplicate@test.local",
                Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // The rejected attempt created nothing.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Assert.Single(
            db.Users.Where(x =>
                x.Email.ToLower() == "pw-duplicate@test.local"));
    }

    [Fact(DisplayName =
        "A contact cannot be added to a soft-deleted company")]
    public async Task SoftDeletedCompany_IsRejected()
    {
        var adminToken = await LoginAsAdminAsync();

        var deletedCompanyId = Guid.NewGuid();

        await WithScopeDbContextAsync(db =>
        {
            db.ClientCompanies.Add(
                new PROManagementSystem.Domain
                    .Entities.ClientCompany
                {
                    Id = deletedCompanyId,
                    CompanyName = "Deleted Co",
                    Email = "deleted-co@test.local",
                    Phone = "+910000000099",
                    Address = "Nowhere",
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            db.SaveChanges();
        });

        var response = await AuthClient.SendAsync(Bearer(
            HttpMethod.Post,
            $"/api/v1/admin/clients/{deletedCompanyId}/contacts",
            new
            {
                fullName = "Ghost Contact",
                email = "pw-deleted-co@test.local",
                password = Password
            },
            adminToken));

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}