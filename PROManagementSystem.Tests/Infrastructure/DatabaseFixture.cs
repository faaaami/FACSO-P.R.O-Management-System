using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Starts a real PostgreSQL Testcontainer and boots the full web host via
/// WebApplicationFactory, applying all existing migrations. Shared across
/// tests via the xUnit collection defined in <see cref="DatabaseCollection"/>.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string TestJwtSecret =
        "day5-test-secret-key-0123456789abcdef0123456789abcdef";
    public const string TestJwtIssuer = "PROManagementSystem";
    public const string TestJwtAudience = "PROManagementSystem.Client";

    public PostgreSqlContainer Container { get; private set; } = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("promanagement_test")
            .WithUsername("postgres")
            .WithPassword("test-password")
            .Build();

        await Container.StartAsync();

        _connectionString = Container.GetConnectionString();

        Factory = new TestApplicationFactory(_connectionString);

        // Apply all existing migrations so the schema matches production.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}

/// <summary>Groups everything into one shared database+host collection.</summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "DatabaseCollection";
}

internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string? _storageRootOverride;
    private string? _autoCreatedStorageRoot;
    private string? _resolvedStorageRoot;

    /// <summary>Absolute storage root used by this factory's host.</summary>
    internal string StorageRoot => _resolvedStorageRoot
        ?? throw new InvalidOperationException(
            "Factory host has not been configured yet.");

    public TestApplicationFactory(
        string connectionString,
        string? storageRoot = null)
    {
        _connectionString = connectionString;
        _storageRootOverride = storageRoot;
    }

    protected override void ConfigureWebHost(
        Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Every factory run persists uploaded files into a unique temp
        // directory, so extraction writes never leak into the repo and each
        // host can be verified against its own storage root.
        var storageRoot = _storageRootOverride
            ?? (_autoCreatedStorageRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    $"proms-storage-{Guid.NewGuid():N}"));

        _resolvedStorageRoot = storageRoot;

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            // Bounded pool + short idle lifetime keep leaked test hosts from
            // exhausting the Postgres connection budget during full-suite
            // runs (53300 "too many clients" if unbound).
            var pooledConnection =
                $"{_connectionString};Maximum Pool Size=10;" +
                "Connection Idle Lifetime=20";

            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = pooledConnection,
                // Redis is configured lazily at startup registration; a
                // dummy value keeps startup from throwing. SignalR backplane
                // is not exercised by these API tests.
                ["ConnectionStrings:Redis"] = "localhost:6399",
                // The API project's appsettings.Local.json (loaded by Program.cs)
                // is present on the test host content root and can select the
                // OpenRouter provider, endpoint, model and API key for local
                // development. Isolate the suite from that developer config:
                // pin the extractor to the deterministic Mock provider and blank
                // the OpenRouter dev settings, so tests never resolve the real
                // provider or perform a network call. Per-test overrides are
                // added later and still win.
                ["Extraction:Provider"] = "Mock",
                ["Extraction:OpenRouter:Endpoint"] = string.Empty,
                ["Extraction:OpenRouter:Model"] = string.Empty,
                ["Extraction:OpenRouter:ApiKey"] = string.Empty,
                // Tests never run the dev-only mock seeding hook. The API
                // project's appsettings.Local.json (loaded by Program.cs) is
                // present on the test host content root and would otherwise
                // enable it, running the seeder before migrations apply.
                ["Seeding:EnableMockClientData"] = "false",
                // The rate limiter state lives for the whole shared test host,
                // so all auth traffic across the suite shares one partition.
                // Keep the base limit effectively unbounded here; dedicated
                // rate-limit tests derive their own host with small limits.
                ["RateLimiting:Authentication:PermitLimit"] = "100000",
                ["RateLimiting:Authentication:WindowSeconds"] = "3600",
                ["RateLimiting:Authentication:QueueLimit"] = "0",
                ["RenewalAutomation:UserId"] =
                    "00000000-0000-0000-0000-0000000000aa",
                ["Jwt:Secret"] = DatabaseFixture.TestJwtSecret,
                ["Jwt:Issuer"] = DatabaseFixture.TestJwtIssuer,
                ["Jwt:Audience"] = DatabaseFixture.TestJwtAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["Storage:RootPath"] = storageRoot,
                ["Razorpay:KeyId"] = FakeRazorpayService.FakeKeyId,
                ["Razorpay:KeySecret"] = "test-key-secret",
                ["Razorpay:WebhookSecret"] = FakeRazorpayService.FakeWebhookSecret
            });
        });

        builder.ConfigureServices(services =>
        {
            // Decouple the realtime/signalr backend from Redis during API
            // tests so status/assignment changes are side-effect free.
            services.AddScoped<IRealtimeNotificationService,
                NoOpRealtimeNotificationService>();

            // Replace the live Razorpay HTTP client with a deterministic fake
            // so client create-order tests never hit the network.
            services.AddScoped<IRazorpayService, FakeRazorpayService>();

            // Capture account emails (invitations, resets, verification) so
            // tests can follow the emailed links without an SMTP server.
            services.AddSingleton<CapturingEmailSender>();
            services.AddSingleton<IEmailSender>(
                provider => provider.GetRequiredService<CapturingEmailSender>());

            // The app registers NpgsqlDataSource at provider-build time from
            // configuration; ensure it points at the test container so the
            // Dapper-backed read repositories see the seeded data.
            services.AddSingleton(
                NpgsqlDataSource.Create(
                    $"{_connectionString};Maximum Pool Size=10;" +
                    "Connection Idle Lifetime=20"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (_autoCreatedStorageRoot is not null)
        {
            try
            {
                Directory.Delete(
                    _autoCreatedStorageRoot,
                    recursive: true);
            }
            catch
            {
                // Best-effort temp cleanup: a failure here must not fail
                // the test suite.
            }
        }

        base.Dispose(disposing);
    }
}
