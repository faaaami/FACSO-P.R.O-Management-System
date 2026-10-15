using Microsoft.Extensions.Configuration;
using PROManagementSystem.Api.Common;
using Xunit;

namespace PROManagementSystem.Tests.Security;

/// <summary>
/// The committed configuration must not be bootable in Production on its own:
/// every secret comes from the environment.
/// </summary>
public sealed class StartupConfigurationValidatorTests
{
    private static string ApiProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "PROManagementSystem");
            if (File.Exists(Path.Combine(candidate, "appsettings.Production.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("API project directory not found.");
    }

    private static IConfiguration CommittedProductionConfiguration(
        IDictionary<string, string?>? environment = null)
    {
        var root = ApiProjectDirectory();

        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, "appsettings.json"))
            .AddJsonFile(Path.Combine(root, "appsettings.Production.json"))
            .AddInMemoryCollection(environment ?? new Dictionary<string, string?>())
            .Build();
    }

    [Fact(DisplayName =
        "Committed Production settings contain only placeholders and fail validation")]
    public void CommittedProductionSettings_AreRejected()
    {
        var problems = StartupConfigurationValidator.FindProblems(
            CommittedProductionConfiguration());

        foreach (var key in new[]
                 {
                     "ConnectionStrings:DefaultConnection",
                     "Jwt:Secret",
                     "Smtp:Host",
                     "Razorpay:KeySecret",
                     "App:FrontendBaseUrl"
                 })
        {
            Assert.Contains(problems, problem => problem.StartsWith(key));
        }

        Assert.Contains(problems, problem => problem.StartsWith("Cors:AllowedOrigins"));
    }

    [Fact(DisplayName = "A fully configured environment passes validation")]
    public void FullyConfigured_Passes()
    {
        var problems = StartupConfigurationValidator.FindProblems(
            CommittedProductionConfiguration(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=db;Database=pro;Username=pro;Password=x",
                ["ConnectionStrings:Redis"] = "redis:6379",
                ["Jwt:Secret"] = new string('k', 48),
                ["App:FrontendBaseUrl"] = "https://pro.example.test",
                ["Cors:AllowedOrigins:0"] = "https://pro.example.test",
                ["Smtp:Host"] = "smtp.example.test",
                ["Smtp:FromAddress"] = "noreply@example.test",
                ["Razorpay:KeyId"] = "id",
                ["Razorpay:KeySecret"] = "secret",
                ["Razorpay:WebhookSecret"] = "hook",
                ["Storage:RootPath"] = "/data/storage"
            }));

        Assert.Empty(problems);
    }

    [Fact(DisplayName = "A short JWT secret is rejected")]
    public void ShortJwtSecret_IsRejected()
    {
        var problems = StartupConfigurationValidator.FindProblems(
            CommittedProductionConfiguration(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "too-short"
            }));

        Assert.Contains(problems, problem => problem.Contains("at least 32"));
    }
}
