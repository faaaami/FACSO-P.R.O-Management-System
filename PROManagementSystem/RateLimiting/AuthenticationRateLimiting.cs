using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PROManagementSystem.Api.RateLimiting;

/// <summary>
/// Configuration for the anonymous authentication rate-limit policy. Values
/// live under <c>RateLimiting:Authentication</c>. Every value is validated on
/// startup: missing, non-numeric, or non-positive input falls back to a safe
/// positive default so a bad configuration can never produce an unlimited
/// authentication endpoint.
/// </summary>
public sealed class AuthenticationRateLimitOptions
{
    public const string SectionName = "RateLimiting:Authentication";

    public const int DefaultPermitLimit = 10;

    public const int DefaultWindowSeconds = 60;

    public const int DefaultQueueLimit = 0;

    public int PermitLimit { get; set; } = DefaultPermitLimit;

    public int WindowSeconds { get; set; } = DefaultWindowSeconds;

    public int QueueLimit { get; set; } = DefaultQueueLimit;
}

public static class AuthenticationRateLimiting
{
    /// <summary>Name applied to the auth controller's endpoints.</summary>
    public const string PolicyName = "Authentication";

    public const string ErrorCode = "RATE_LIMIT_EXCEEDED";

    public const string ErrorMessage =
        "Too many requests. Please try again later.";

    public static AuthenticationRateLimitOptions Resolve(
        IConfiguration configuration)
    {
        var section = configuration.GetSection(
            AuthenticationRateLimitOptions.SectionName);

        return new AuthenticationRateLimitOptions
        {
            PermitLimit = ResolveInt(
                section[nameof(AuthenticationRateLimitOptions.PermitLimit)],
                AuthenticationRateLimitOptions.DefaultPermitLimit,
                minimum: 1),
            WindowSeconds = ResolveInt(
                section[nameof(AuthenticationRateLimitOptions.WindowSeconds)],
                AuthenticationRateLimitOptions.DefaultWindowSeconds,
                minimum: 1),
            QueueLimit = ResolveInt(
                section[nameof(AuthenticationRateLimitOptions.QueueLimit)],
                AuthenticationRateLimitOptions.DefaultQueueLimit,
                minimum: 0)
        };
    }

    public static FixedWindowRateLimiterOptions ToLimiterOptions(
        AuthenticationRateLimitOptions options)
        => new()
        {
            PermitLimit = options.PermitLimit,
            Window = TimeSpan.FromSeconds(options.WindowSeconds),
            QueueLimit = options.QueueLimit,
            AutoReplenishment = true
        };

    /// <summary>
    /// Anonymous authentication is partitioned by client IP. A missing
    /// address shares a single conservative "unknown" partition rather than
    /// bypassing the limit.
    /// </summary>
    public static string ResolvePartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;

        return address is null ? "unknown" : address.ToString();
    }

    private static int ResolveInt(string? raw, int fallback, int minimum)
        => int.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) && value >= minimum
            ? value
            : fallback;
}
