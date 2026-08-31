using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Infrastructure.Persistence;
using Xunit;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Base for all integration tests. Provides:
///  - <see cref="ScopeClient"/>: uses the TestIdentity scheme to impersonate
///    any principal without minting JWTs (scope/authorization isolation).
///  - <see cref="AuthClient"/>: uses the real JwtBearer pipeline (real-auth
///    tests).
/// Each test starts from a freshly truncated + reseeded database.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    protected WebApplicationFactory<Program> Factory => _fixture.Factory;

    /// <summary>Client authenticated via the TestIdentity scheme.</summary>
    protected HttpClient ScopeClient { get; private set; } = null!;

    /// <summary>Client using the production JWT pipeline.</summary>
    protected HttpClient AuthClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
        await SeedAsync();

        ScopeClient = BuildScopeClient();
        AuthClient = Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        ScopeClient?.Dispose();
        AuthClient?.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // Database lifecycle
    // ------------------------------------------------------------------

    private async Task ResetDatabaseAsync()
    {
        var db = await CreateScopeDbContextAsync();

        const string tables =
            "\"renewal_task_histories\", \"renewal_step_logs\", " +
            "\"renewal_tasks\", \"document_dependencies\", " +
            "\"document_extractions\", \"document_versions\", " +
            "\"notifications\", \"documents\", \"employees\", " +
            "\"client_entities\", \"client_companies\", \"audit_logs\", " +
            "\"users\"";

        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {tables} CASCADE");

        await db.DisposeAsync();
    }

    private Task SeedAsync()
        => WithScopeDbContextAsync(TestDataSeeder.Seed);

    // ------------------------------------------------------------------
    // Clients
    // ------------------------------------------------------------------

    private HttpClient BuildScopeClient()
    {
        return Factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme =
                                TestIdentityService.SchemeName;
                            options.DefaultChallengeScheme =
                                TestIdentityService.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions,
                            TestIdentityService>(
                            TestIdentityService.SchemeName, _ => { });
                });
            })
            .CreateClient();
    }

    // ------------------------------------------------------------------
    // Scope request helpers (inject test identity header)
    // ------------------------------------------------------------------

    protected HttpRequestMessage ScopeGet(
        string url,
        Guid userId,
        string role = "Agent",
        string? clientCompanyId = null)
        => ApplyScope(new HttpRequestMessage(HttpMethod.Get, url),
            userId, role, clientCompanyId);

    protected HttpRequestMessage ScopePost(
        string url,
        object? body,
        Guid userId,
        string role = "Agent",
        string? clientCompanyId = null)
        => ApplyScope(JsonRequest(HttpMethod.Post, url, body),
            userId, role, clientCompanyId);

    protected HttpRequestMessage ScopePatch(
        string url,
        object? body,
        Guid userId,
        string role = "Agent",
        string? clientCompanyId = null)
        => ApplyScope(JsonRequest(HttpMethod.Patch, url, body),
            userId, role, clientCompanyId);

    private static HttpRequestMessage ApplyScope(
        HttpRequestMessage request,
        Guid userId,
        string role,
        string? clientCompanyId)
    {
        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(
                userId, role, clientCompanyId));
        return request;
    }

    private static HttpRequestMessage JsonRequest(
        HttpMethod method,
        string url,
        object? body)
    {
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content =
                System.Net.Http.Json.JsonContent.Create(body);
        }

        return request;
    }

    protected static HttpRequestMessage AuthGet(
        string url,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // ------------------------------------------------------------------
    // Response helpers
    // ------------------------------------------------------------------

    protected static async Task EnsureOk(
        HttpResponseMessage response,
        System.Net.HttpStatusCode expected =
            System.Net.HttpStatusCode.OK)
        => Assert.Equal(expected, response.StatusCode);

    protected static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    // ------------------------------------------------------------------
    // DbContext helpers
    // ------------------------------------------------------------------

    protected async Task<ApplicationDbContext> CreateScopeDbContextAsync()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
    }

    protected async Task WithScopeDbContextAsync(
        Action<ApplicationDbContext> action)
    {
        var db = await CreateScopeDbContextAsync();
        try
        {
            action(db);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }
}
