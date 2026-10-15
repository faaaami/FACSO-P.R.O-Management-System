using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Auth.Commands.Login;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Auth;

/// <summary>Helpers for tests that drive the real JWT and email flows.</summary>
public abstract class AuthFlowTestBase : IntegrationTestBase
{
    protected const string AdminEmail = "admin@test.local";
    protected const string AgentAEmail = "agent-a@test.local";
    protected const string Password = TestDataSeeder.TestLoginPassword;

    protected AuthFlowTestBase(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    protected Task<HttpResponseMessage> LoginAttemptAsync(
        string email,
        string password)
        => AuthClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

    protected async Task<LoginResponseDto> LoginAsync(
        string email,
        string password)
    {
        var response = await LoginAttemptAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadJsonAsync<ApiResponse<LoginResponseDto>>(response);

        Assert.NotNull(envelope?.Data);

        return envelope!.Data;
    }

    protected Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return AuthClient.SendAsync(request);
    }

    protected Task<HttpResponseMessage> PostAnonymousAsync(string url, object body)
        => AuthClient.PostAsJsonAsync(url, body);

    protected static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return node?["error"]?["code"]?.GetValue<string>();
    }

    protected static async Task<JsonNode> DataAsync(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return node?["data"]
            ?? throw new InvalidOperationException("Response has no data.");
    }
}
