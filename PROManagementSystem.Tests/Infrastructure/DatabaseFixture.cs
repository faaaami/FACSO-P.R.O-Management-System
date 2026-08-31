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

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder()
            .WithDatabase("promanagement_test")
            .WithUsername("postgres")
            .WithPassword("test-password")
            .Build();

        await Container.StartAsync();

        Factory = new TestApplicationFactory(Container.GetConnectionString());

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

    public TestApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(
        Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                // Redis is configured lazily at startup registration; a
                // dummy value keeps startup from throwing. SignalR backplane
                // is not exercised by these API tests.
                ["ConnectionStrings:Redis"] = "localhost:6399",
                ["RenewalAutomation:UserId"] =
                    "00000000-0000-0000-0000-0000000000aa",
                ["Jwt:Secret"] = DatabaseFixture.TestJwtSecret,
                ["Jwt:Issuer"] = DatabaseFixture.TestJwtIssuer,
                ["Jwt:Audience"] = DatabaseFixture.TestJwtAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Decouple the realtime/signalr backend from Redis during API
            // tests so status/assignment changes are side-effect free.
            services.AddScoped<IRealtimeNotificationService,
                NoOpRealtimeNotificationService>();

            // The app registers NpgsqlDataSource at provider-build time from
            // configuration; ensure it points at the test container so the
            // Dapper-backed read repositories see the seeded data.
            services.AddSingleton(
                NpgsqlDataSource.Create(_connectionString));
        });
    }
}
