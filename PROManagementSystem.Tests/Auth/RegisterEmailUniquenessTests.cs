using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>
/// Verifies case-insensitive and soft-delete-aware email uniqueness on
/// the public registration endpoint.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RegisterEmailUniquenessTests : IntegrationTestBase
{
    public RegisterEmailUniquenessTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private const string Password = "TestPassword123!";

    [Fact(DisplayName =
        "Different casing of the same email returns 409 CONFLICT")]
    public async Task DifferentCasing_Returns409()
    {
        var body = new
        {
            fullName = "Casing Test",
            email = "casing@test.local",
            password = Password,
            phone = (string?)null
        };

        var first = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var bodyUpper = new
        {
            fullName = "Casing Test Upper",
            email = "CASING@TEST.LOCAL",
            password = Password,
            phone = (string?)null
        };

        var second = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", bodyUpper);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var json = await second.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.NotNull(json);
        Assert.False(json.Success);
        Assert.Equal("CONFLICT", json.Error!.Code);
        Assert.Contains(
            "already exists",
            json.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "Registration against a soft-deleted user's email returns 200 OK")]
    public async Task SoftDeletedEmail_CanReRegister()
    {
        var email = "softdel@test.local";

        var body = new
        {
            fullName = "Soft Delete Original",
            email,
            password = Password,
            phone = (string?)null
        };

        var first = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await SoftDeleteUserByEmail(email);

        var bodyAgain = new
        {
            fullName = "Soft Delete Re-register",
            email,
            password = Password,
            phone = (string?)null
        };

        var second = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", bodyAgain);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact(DisplayName =
        "Soft-deleted email with different casing can also re-register")]
    public async Task SoftDeletedEmail_DifferentCasing_CanReRegister()
    {
        var email = "softdelcase@test.local";

        var body = new
        {
            fullName = "Soft Delete Case Original",
            email,
            password = Password,
            phone = (string?)null
        };

        var first = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await SoftDeleteUserByEmail(email);

        var bodyUpper = new
        {
            fullName = "Soft Delete Case Upper",
            email = "SOFTDELCASE@TEST.LOCAL",
            password = Password,
            phone = (string?)null
        };

        var second = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", bodyUpper);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact(DisplayName =
        "Active duplicate email still returns 409")]
    public async Task ActiveDuplicate_Returns409()
    {
        var body = new
        {
            fullName = "Duplicate Test",
            email = "dup@test.local",
            password = Password,
            phone = (string?)null
        };

        var first = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var bodyAgain = new
        {
            fullName = "Duplicate Test Again",
            email = "dup@test.local",
            password = Password,
            phone = (string?)null
        };

        var second = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", bodyAgain);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName =
        "Existing seeded email returns 409")]
    public async Task ExistingSeededEmail_Returns409()
    {
        var body = new
        {
            fullName = "Admin Re-register",
            email = "admin@test.local",
            password = Password,
            phone = (string?)null
        };

        var response = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName =
        "Email with leading/trailing spaces is normalized before duplicate check")]
    public async Task EmailWithSpaces_NormalizedBeforeCheck()
    {
        var body = new
        {
            fullName = "Space Test",
            email = "spacetest@test.local",
            password = Password,
            phone = (string?)null
        };

        var first = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var bodyPadded = new
        {
            fullName = "Space Test Padded",
            email = "  SpaceTest@Test.Local  ",
            password = Password,
            phone = (string?)null
        };

        var second = await AuthClient.PostAsJsonAsync(
            "/api/v1/auth/register", bodyPadded);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task SoftDeleteUserByEmail(string email)
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

    private sealed class ApiResponse
    {
        public bool Success { get; set; }
        public ApiError? Error { get; set; }
    }

    private sealed class ApiError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
