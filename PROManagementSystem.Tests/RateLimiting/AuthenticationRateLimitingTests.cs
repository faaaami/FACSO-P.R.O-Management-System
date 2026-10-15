using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Api.RateLimiting;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.RateLimiting;

/// <summary>
/// Focused coverage for audit finding H2: the anonymous authentication
/// endpoints are protected by a per-IP fixed-window rate limit. Each test
/// derives its own host with a small deterministic limit and a synthetic
/// client IP (TEST-NET-3), so no test waits on the clock or uses real data.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthenticationRateLimitingTests : IntegrationTestBase
{
    private const int PermitLimit = 3;

    private const string LoginPath = "/api/v1/auth/login";
    private const string AcceptInvitationPath = "/api/v1/auth/accept-invitation";
    private const string ResendVerificationPath = "/api/v1/auth/resend-verification";
    private const string ForgotPasswordPath = "/api/v1/auth/forgot-password";
    private const string ResetPasswordPath = "/api/v1/auth/reset-password";
    private const string VerifyEmailPath = "/api/v1/auth/verify-email";
    private const string RefreshPath = "/api/v1/auth/refresh";

    private const string UnknownEmail = "rate-limit-unknown@test.local";
    private const string ProbePassword = "ProbePass123!";

    private int _ipCounter;

    public AuthenticationRateLimitingTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private sealed class RateLimitedHost : IDisposable
    {
        public RateLimitedHost(WebApplicationFactory<Program> factory)
        {
            Factory = factory;
            Client = factory.CreateClient();
        }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    private RateLimitedHost CreateHost(int permitLimit = PermitLimit)
        => new(Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<AuthenticationRateLimitOptions>(
                    options =>
                    {
                        options.PermitLimit = permitLimit;
                        options.WindowSeconds = 3600;
                        options.QueueLimit = 0;
                    });

                services.AddSingleton<IStartupFilter>(
                    new RemoteIpHeaderStartupFilter());
            });
        }));

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private string NextIp()
        => $"203.0.113.{Interlocked.Increment(ref _ipCounter)}";

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        object body,
        string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        request.Headers.Add(
            RemoteIpHeaderStartupFilter.HeaderName,
            ip);

        return await client.SendAsync(request);
    }

    private async Task AssertEndpointIsRateLimitedAsync(
        string path,
        Func<int, object> bodyFactory)
    {
        using var host = CreateHost();
        var ip = NextIp();

        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            var allowed = await PostAsync(
                host.Client,
                path,
                bodyFactory(attempt),
                ip);

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                allowed.StatusCode);
        }

        var limited = await PostAsync(
            host.Client,
            path,
            bodyFactory(PermitLimit),
            ip);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            limited.StatusCode);
    }

    // ------------------------------------------------------------------
    // Per-endpoint limits
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "H2: login is rate limited and requests below the limit reach it")]
    public Task Login_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            LoginPath,
            _ => new { email = UnknownEmail, password = ProbePassword });

    [Fact(DisplayName = "H2: accept-invitation is rate limited")]
    public Task AcceptInvitation_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            AcceptInvitationPath,
            _ => new
            {
                token = "not-a-real-token",
                newPassword = ProbePassword
            });

    [Fact(DisplayName = "H2: resend-verification is rate limited")]
    public Task ResendVerification_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            ResendVerificationPath,
            _ => new { email = UnknownEmail });

    [Fact(DisplayName = "H2: forgot-password is rate limited")]
    public Task ForgotPassword_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            ForgotPasswordPath,
            _ => new { email = UnknownEmail });

    [Fact(DisplayName = "H2: reset-password is rate limited")]
    public Task ResetPassword_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            ResetPasswordPath,
            _ => new
            {
                token = "not-a-real-token",
                newPassword = ProbePassword
            });

    [Fact(DisplayName = "H2: email verification is rate limited")]
    public Task VerifyEmail_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            VerifyEmailPath,
            _ => new { token = "not-a-real-token" });

    [Fact(DisplayName = "H2: refresh is rate limited")]
    public Task Refresh_IsRateLimited()
        => AssertEndpointIsRateLimitedAsync(
            RefreshPath,
            _ => new { refreshToken = "not-a-real-token" });

    // ------------------------------------------------------------------
    // Partitioning / blast radius
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "H2: partitions are isolated per client IP, so one busy IP does not " +
        "block another")]
    public async Task Partition_IsIsolatedPerClientIp()
    {
        using var host = CreateHost();

        var busyIp = NextIp();
        var otherIp = NextIp();
        var body = new { email = UnknownEmail, password = ProbePassword };

        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            await PostAsync(host.Client, LoginPath, body, busyIp);
        }

        var blocked = await PostAsync(
            host.Client,
            LoginPath,
            body,
            busyIp);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        var unaffected = await PostAsync(
            host.Client,
            LoginPath,
            body,
            otherIp);

        Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            unaffected.StatusCode);
    }

    [Fact(DisplayName =
        "H2: normal authenticated API traffic is not affected by the " +
        "authentication policy")]
    public async Task AuthenticatedTraffic_IsNotRateLimited()
    {
        using var host = CreateHost();

        var token = JwtHelper.MintToken(
            TestDataSeeder.Ids.AgentA,
            "Agent",
            "agent-a@test.local",
            "Agent A",
            clientCompanyId: TestDataSeeder.Ids.CompanyA.ToString());

        var ip = NextIp();

        for (var attempt = 0; attempt < PermitLimit + 3; attempt++)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/agent/me");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            request.Headers.Add(
                RemoteIpHeaderStartupFilter.HeaderName,
                ip);

            var response = await host.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ------------------------------------------------------------------
    // 429 response safety
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "H2: the 429 response is identical for known and unknown accounts " +
        "and does not reveal account existence")]
    public async Task RateLimitedResponse_DoesNotRevealAccountExistence()
    {
        using var host = CreateHost();

        var knownIp = NextIp();
        var unknownIp = NextIp();

        var knownBody = new
        {
            email = "agent-a@test.local",
            password = TestDataSeeder.TestLoginPassword
        };
        var unknownBody = new
        {
            email = UnknownEmail,
            password = TestDataSeeder.TestLoginPassword
        };

        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            await PostAsync(host.Client, LoginPath, knownBody, knownIp);
            await PostAsync(host.Client, LoginPath, unknownBody, unknownIp);
        }

        var knownLimited = await PostAsync(
            host.Client,
            LoginPath,
            knownBody,
            knownIp);
        var unknownLimited = await PostAsync(
            host.Client,
            LoginPath,
            unknownBody,
            unknownIp);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            knownLimited.StatusCode);
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            unknownLimited.StatusCode);

        Assert.True(
            knownLimited.Headers.RetryAfter is not null,
            "429 headers: " + string.Join(
                ", ",
                knownLimited.Headers.Select(
                    h => $"{h.Key}={string.Join('|', h.Value)}")));

        var renderedKnown =
            await knownLimited.Content.ReadAsStringAsync();
        var renderedUnknown =
            await unknownLimited.Content.ReadAsStringAsync();

        Assert.Equal(renderedKnown, renderedUnknown);
        Assert.DoesNotContain(
            "agent-a@test.local",
            renderedKnown,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            UnknownEmail,
            renderedUnknown,
            StringComparison.OrdinalIgnoreCase);
    }
}
