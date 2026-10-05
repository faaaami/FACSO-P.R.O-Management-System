using Microsoft.Extensions.Configuration;
using PROManagementSystem.Api.RateLimiting;
using Xunit;

namespace PROManagementSystem.Tests.RateLimiting;

/// <summary>
/// H2: proves the authentication rate-limit configuration can never resolve
/// to an unlimited endpoint. Missing, non-numeric, or non-positive values
/// fall back to safe positive defaults.
/// </summary>
public sealed class AuthenticationRateLimitOptionsTests
{
    private static AuthenticationRateLimitOptions Resolve(
        params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.ToDictionary(
                    value => value.Key,
                    value => value.Value))
            .Build();

        return AuthenticationRateLimiting.Resolve(configuration);
    }

    [Fact(DisplayName =
        "H2: missing configuration resolves to safe positive defaults")]
    public void MissingConfiguration_UsesSafeDefaults()
    {
        var options = Resolve();

        Assert.True(options.PermitLimit >= 1);
        Assert.True(options.WindowSeconds >= 1);
        Assert.True(options.QueueLimit >= 0);
    }

    [Fact(DisplayName =
        "H2: valid configuration values are honored")]
    public void ValidConfiguration_IsHonored()
    {
        var options = Resolve(
            ("RateLimiting:Authentication:PermitLimit", "7"),
            ("RateLimiting:Authentication:WindowSeconds", "30"),
            ("RateLimiting:Authentication:QueueLimit", "2"));

        Assert.Equal(7, options.PermitLimit);
        Assert.Equal(30, options.WindowSeconds);
        Assert.Equal(2, options.QueueLimit);
    }

    [Theory(DisplayName =
        "H2: non-positive or non-numeric permit limits fall back to a " +
        "positive default")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidPermitLimit_FallsBackToPositiveDefault(
        string? value)
    {
        var options = Resolve(
            ("RateLimiting:Authentication:PermitLimit", value));

        Assert.Equal(
            AuthenticationRateLimitOptions.DefaultPermitLimit,
            options.PermitLimit);
    }

    [Theory(DisplayName =
        "H2: non-positive or non-numeric windows fall back to a positive " +
        "default")]
    [InlineData("0")]
    [InlineData("-30")]
    [InlineData("nope")]
    [InlineData(null)]
    public void InvalidWindow_FallsBackToPositiveDefault(string? value)
    {
        var options = Resolve(
            ("RateLimiting:Authentication:WindowSeconds", value));

        Assert.Equal(
            AuthenticationRateLimitOptions.DefaultWindowSeconds,
            options.WindowSeconds);
    }

    [Theory(DisplayName =
        "H2: negative or non-numeric queue limits fall back to zero")]
    [InlineData("-1")]
    [InlineData("xyz")]
    public void InvalidQueueLimit_FallsBackToZero(string? value)
    {
        var options = Resolve(
            ("RateLimiting:Authentication:QueueLimit", value));

        Assert.Equal(0, options.QueueLimit);
    }
}
