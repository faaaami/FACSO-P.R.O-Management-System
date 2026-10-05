using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Application.Clients.Commands.CreateClientContact;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.ClientTenancy;

/// <summary>
/// Covers email uniqueness for the Admin client-contact endpoint.
///
/// The contact handler stored a normalized email (Trim + ToLowerInvariant) but
/// compared the RAW request value, so a case or whitespace variant slipped past
/// the application check and surfaced as a database unique-constraint failure
/// instead of the intended 409. These tests pin the application-level rejection
/// and the soft-delete reuse rule.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ClientContactEmailUniquenessTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@test.local";

    // Required by the create route but never the subject of this suite: these
    // tests are about the email, so the value only has to be valid.
    private const string ContactPassword = "UniquenessSubjectPw1!";

    private const string BaseEmail = "contact-uniqueness@test.local";

    public ClientContactEmailUniquenessTests(DatabaseFixture fixture)
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

    private Task<HttpResponseMessage> CreateContactAsync(
        string adminToken,
        string email)
        => AuthClient.SendAsync(Bearer(
            HttpMethod.Post,
            $"/api/v1/admin/clients/{TestDataSeeder.Ids.CompanyA}" +
            "/contacts",
            new
            {
                fullName = "Uniqueness Subject",
                email,
                phone = "+910000000021",
                password = ContactPassword
            },
            adminToken));

    /// <summary>
    /// Asserts the application-level conflict contract: 409 with the
    /// "already exists" message, rather than a 500 caused by the database
    /// unique index rejecting the insert.
    /// </summary>
    private static async Task AssertConflictAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiResponse<object>>();

        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.Contains(
            "already exists",
            envelope.Error!.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task SoftDeleteUserByEmailAsync(string email)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u =>
                    u.Email == email.Trim().ToLowerInvariant());

            if (user is not null)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // Cases
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Contact with an exactly duplicated email returns 409")]
    public async Task ExactDuplicate_Returns409()
    {
        var adminToken = await LoginAsAdminAsync();

        var first = await CreateContactAsync(adminToken, BaseEmail);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await AssertConflictAsync(
            await CreateContactAsync(adminToken, BaseEmail));
    }

    [Fact(DisplayName =
        "Contact with a different-cased duplicate email returns 409")]
    public async Task CaseVariant_Returns409()
    {
        var adminToken = await LoginAsAdminAsync();

        var first = await CreateContactAsync(adminToken, BaseEmail);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await AssertConflictAsync(
            await CreateContactAsync(
                adminToken,
                BaseEmail.ToUpperInvariant()));
    }

    [Fact(DisplayName =
        "Contact with a whitespace-padded duplicate email returns 409")]
    public async Task WhitespaceVariant_Returns409()
    {
        var adminToken = await LoginAsAdminAsync();

        var first = await CreateContactAsync(adminToken, BaseEmail);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await AssertConflictAsync(
            await CreateContactAsync(
                adminToken,
                $"  {BaseEmail.ToUpperInvariant()}  "));
    }

    [Fact(DisplayName =
        "Contact email already held by a STAFF account returns 409")]
    public async Task DuplicateOfStaffAccount_Returns409()
    {
        var adminToken = await LoginAsAdminAsync();

        await AssertConflictAsync(
            await CreateContactAsync(
                adminToken,
                "agent-a@test.local"));
    }

    [Fact(DisplayName =
        "A soft-deleted contact's email can be reused, including with " +
        "different casing and padding")]
    public async Task SoftDeletedEmail_CanBeReused()
    {
        var adminToken = await LoginAsAdminAsync();

        var first = await CreateContactAsync(adminToken, BaseEmail);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await SoftDeleteUserByEmailAsync(BaseEmail);

        var reuse = await CreateContactAsync(
            adminToken,
            $"  {BaseEmail.ToUpperInvariant()}  ");

        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);

        var envelope = await reuse.Content
            .ReadFromJsonAsync<
                ApiResponse<CreateClientContactResponseDto>>();

        Assert.NotNull(envelope?.Data);

        // The stored value must be normalized, not the padded raw input.
        Assert.Equal(BaseEmail, envelope!.Data.Email);
    }
}