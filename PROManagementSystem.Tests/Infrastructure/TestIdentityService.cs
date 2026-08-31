using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Test-only identity used to efficiently exercise scope/authorization tests
/// without minting JWTs for every request. Reads identity claims from the
/// <c>X-Test-Identity</c> header (JSON:
/// <c>{ "userId": "guid", "role": "Agent", "clientCompanyId": "guid" }</c>).
/// This scheme is registered ONLY on the scope-test client and does not
/// affect the production JWT pipeline used by the real-auth client.
/// </summary>
public sealed class TestIdentityService : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestIdentity";

    private const string HeaderName = "X-Test-Identity";

    public TestIdentityService(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Context.Request.Headers.TryGetValue(
                HeaderName, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        TestIdentityPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<TestIdentityPayload>(
                raw.ToString(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return Task.FromResult(
                AuthenticateResult.Fail(
                    "Invalid test identity payload."));
        }

        if (payload is null ||
            !Guid.TryParse(payload.UserId, out var userId))
        {
            return Task.FromResult(
                AuthenticateResult.Fail(
                    "Test identity is missing a valid userId."));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, payload.Role ?? "Agent")
        };

        if (!string.IsNullOrWhiteSpace(payload.ClientCompanyId) &&
            Guid.TryParse(payload.ClientCompanyId, out var clientCompanyId))
        {
            claims.Add(new Claim(
                "client_company_id", clientCompanyId.ToString()));
        }

        var identity = new ClaimsIdentity(
            claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal, SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }

    public static string HeaderPayload(
        Guid userId,
        string role = "Agent",
        string? clientCompanyId = null)
    {
        var payload = new TestIdentityPayload
        {
            UserId = userId.ToString(),
            Role = role,
            ClientCompanyId = clientCompanyId
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class TestIdentityPayload
    {
        public string? UserId { get; set; }

        public string? Role { get; set; }

        public string? ClientCompanyId { get; set; }
    }
}
