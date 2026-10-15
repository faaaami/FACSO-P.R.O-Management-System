namespace PROManagementSystem.Api.Common;

/// <summary>
/// Fails startup outside Development when a required setting is missing or
/// still holds a placeholder (an empty value or one starting with "&lt;", as
/// used in appsettings.Production.json). Values are never echoed, only the
/// names of the missing keys.
/// </summary>
public static class StartupConfigurationValidator
{
    public const int MinimumJwtSecretLength = 32;

    private static readonly string[] RequiredKeys =
    {
        "ConnectionStrings:DefaultConnection",
        "ConnectionStrings:Redis",
        "Jwt:Secret",
        "Jwt:Issuer",
        "Jwt:Audience",
        "App:FrontendBaseUrl",
        "Smtp:Host",
        "Smtp:FromAddress",
        "Razorpay:KeyId",
        "Razorpay:KeySecret",
        "Razorpay:WebhookSecret",
        "RenewalAutomation:UserId",
        "Storage:RootPath"
    };

    public static IReadOnlyList<string> FindProblems(IConfiguration configuration)
    {
        var problems = new List<string>();

        foreach (var key in RequiredKeys)
        {
            if (IsPlaceholder(configuration[key]))
            {
                problems.Add($"{key} is not set");
            }
        }

        var secret = configuration["Jwt:Secret"];
        if (!IsPlaceholder(secret) &&
            secret!.Length < MinimumJwtSecretLength)
        {
            problems.Add(
                $"Jwt:Secret must be at least {MinimumJwtSecretLength} characters");
        }

        var frontend = configuration["App:FrontendBaseUrl"];
        if (!IsPlaceholder(frontend) &&
            !Uri.TryCreate(frontend, UriKind.Absolute, out _))
        {
            problems.Add("App:FrontendBaseUrl must be an absolute URL");
        }

        if (GetAllowedOrigins(configuration).Length == 0)
        {
            problems.Add("Cors:AllowedOrigins must list at least one origin");
        }

        return problems;
    }

    public static void ValidateOrThrow(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var problems = FindProblems(configuration);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The application is not configured for " +
                $"{environment.EnvironmentName}: " +
                string.Join("; ", problems) +
                ". See README.md for the required environment variables.");
        }
    }

    public static string[] GetAllowedOrigins(IConfiguration configuration)
        => configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !IsPlaceholder(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .ToArray()
            ?? Array.Empty<string>();

    public static bool IsPlaceholder(string? value)
        => string.IsNullOrWhiteSpace(value) ||
           value.TrimStart().StartsWith('<');
}
