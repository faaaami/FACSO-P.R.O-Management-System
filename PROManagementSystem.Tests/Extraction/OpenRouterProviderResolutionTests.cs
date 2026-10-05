using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI;
using PROManagementSystem.Infrastructure.AI.DocumentExtractionService;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7C.3.1: provider selection and boot safety. The Mock provider remains
/// the default and the Local provider is untouched; the OpenRouter provider is
/// opt-in, resolves case-insensitively, and fails with a clear configuration
/// error (never a silent Mock fallback) when selected without complete
/// configuration. No HTTP call is made by these tests.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OpenRouterProviderResolutionTests : IntegrationTestBase
{
    public OpenRouterProviderResolutionTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName =
        "The default provider remains the Mock extractor")]
    public void Default_ResolvesMock()
    {
        Assert.Equal(
            typeof(MockDocumentExtractionService),
            ResolveExtractorType(null));
    }

    [Theory(DisplayName =
        "Mock selection remains case-insensitive")]
    [InlineData("Mock")]
    [InlineData("mock")]
    [InlineData("MOCK")]
    public void Mock_IsCaseInsensitive(string provider)
    {
        Assert.Equal(
            typeof(MockDocumentExtractionService),
            ResolveExtractorType(provider));
    }

    [Theory(DisplayName =
        "Local selection remains case-insensitive and unchanged")]
    [InlineData("Local")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public void Local_IsCaseInsensitive(string provider)
    {
        Assert.Equal(
            typeof(LocalDocumentExtractionService),
            ResolveExtractorType(provider));
    }

    [Theory(DisplayName =
        "OpenRouter selection resolves the OpenRouter provider, any casing")]
    [InlineData("OpenRouter")]
    [InlineData("openrouter")]
    [InlineData("OPENROUTER")]
    public void OpenRouter_ResolvesProvider(string provider)
    {
        Assert.Equal(
            typeof(OpenRouterDocumentExtractionService),
            ResolveExtractorType(provider, FullOpenRouterConfig));
    }

    [Fact(DisplayName =
        "Selecting OpenRouter with incomplete configuration fails clearly, not Mock")]
    public void OpenRouter_IncompleteConfiguration_FailsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ResolveExtractorType("OpenRouter"));

        Assert.Contains(
            "OpenRouter",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Endpoint",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "ApiKey",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IReadOnlyDictionary<string, string?> FullOpenRouterConfig =>
        new Dictionary<string, string?>
        {
            ["Extraction:OpenRouter:Endpoint"] =
                "https://openrouter.example.test/v1",
            ["Extraction:OpenRouter:Model"] = "test-model",
            ["Extraction:OpenRouter:MaxTokens"] = "512",
            ["Extraction:OpenRouter:RequestTimeoutSeconds"] = "30",
            ["Extraction:OpenRouter:MaxRetries"] = "2",
            ["Extraction:OpenRouter:ApiKey"] =
                "test-only-key-not-a-real-secret",
        };

    private Type ResolveExtractorType(
        string? provider,
        IReadOnlyDictionary<string, string?>? extra = null)
    {
        using var factory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrides =
                    new Dictionary<string, string?>();

                if (provider is not null)
                {
                    overrides["Extraction:Provider"] = provider;
                }

                if (extra is not null)
                {
                    foreach (var pair in extra)
                    {
                        overrides[pair.Key] = pair.Value;
                    }
                }

                config.AddInMemoryCollection(overrides);
            }));

        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<IDocumentExtractionService>()
            .GetType();
    }
}

/// <summary>
/// Task 7C.3.1: the DI resilience policy for the OpenRouter typed client. A
/// handler filter replaces the primary transport so the retry behavior can be
/// asserted deterministically without any network call: 429 and 5xx are
/// retried up to MaxRetries, ordinary 4xx are never retried.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OpenRouterResilienceTests : IntegrationTestBase
{
    public OpenRouterResilienceTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName =
        "HTTP 429 is retried MaxRetries times then degrades neutrally")]
    public async Task TooManyRequests_IsRetriedThenNeutral()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.TooManyRequests,
            "{}");

        var result = await ExtractAsync(handler);

        Assert.Null(result.Type);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact(DisplayName =
        "HTTP 5xx is retried MaxRetries times then degrades neutrally")]
    public async Task ServerError_IsRetriedThenNeutral()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.BadGateway,
            "{}");

        var result = await ExtractAsync(handler);

        Assert.Null(result.Type);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact(DisplayName =
        "An ordinary 4xx is never retried")]
    public async Task ClientError_IsNotRetried()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.BadRequest,
            "{}");

        var result = await ExtractAsync(handler);

        Assert.Null(result.Type);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory(DisplayName =
        "Ordinary 4xx statuses are never retried")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ClientErrorStatuses_AreNotRetried(
        HttpStatusCode statusCode)
    {
        var handler = new RecordingHttpMessageHandler(statusCode, "{}");

        var result = await ExtractAsync(handler);

        Assert.Null(result.Type);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact(DisplayName =
        "HTTP 429 then success retries and returns the parsed suggestion")]
    public async Task TooManyRequests_ThenSuccess_Retries()
    {
        var handler = new RecordingHttpMessageHandler(
            Sequenced(HttpStatusCode.TooManyRequests, SuccessResponse()));

        var result = await ExtractAsync(handler);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact(DisplayName =
        "HTTP 503 then success retries and returns the parsed suggestion")]
    public async Task ServerError_ThenSuccess_Retries()
    {
        var handler = new RecordingHttpMessageHandler(
            Sequenced(HttpStatusCode.ServiceUnavailable, SuccessResponse()));

        var result = await ExtractAsync(handler);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact(DisplayName =
        "A transport failure then success retries and returns the suggestion")]
    public async Task TransportFailure_ThenSuccess_Retries()
    {
        var handler = new RecordingHttpMessageHandler(
            Sequenced(
                exception: new HttpRequestException("simulated transport"),
                next: SuccessResponse()));

        var result = await ExtractAsync(handler);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact(DisplayName =
        "MaxRetries = 0 sends exactly one request even for a retryable failure")]
    public async Task MaxRetriesZero_SendsOneRequest()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            "{}");

        var result = await ExtractAsync(handler, maxRetries: 0);

        Assert.Null(result.Type);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact(DisplayName =
        "A permanent 5xx cannot exceed MaxRetries + 1 bounded attempts")]
    public async Task PermanentServerError_IsBoundedByMaxRetries()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "{}");

        var result = await ExtractAsync(handler, maxRetries: 5);

        Assert.Null(result.Type);
        Assert.Equal(6, handler.RequestCount);
    }

    [Fact(DisplayName =
        "An oversized 2xx body degrades neutrally and is never retried")]
    public async Task OversizedResponse_IsNotRetried()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            "{\"choices\":[],\"padding\":\"" + new string('x', 10_000) + "\"}");

        var result = await ExtractAsync(
            handler,
            maxRetries: 2,
            maxResponseBytes: 512);

        Assert.Null(result.Type);

        // HttpClient enforces MaxResponseContentBufferSize while buffering the
        // body, after the resilience pipeline has returned, so an oversized
        // body must not be re-requested (no repeated paid model calls).
        Assert.Equal(1, handler.RequestCount);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private const string LogicalExtraction =
        "{\"documentType\":\"Passport\",\"documentNumber\":\"P-90321\"," +
        "\"ownerType\":\"Employee\"," +
        "\"details\":{\"passportNumber\":\"P-90321\"},\"confidence\":0.9}";

    private static HttpResponseMessage SuccessResponse()
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":" +
                System.Text.Json.JsonSerializer.Serialize(LogicalExtraction) +
                "}}]}",
                System.Text.Encoding.UTF8,
                "application/json"),
        };

    private static Func<
        HttpRequestMessage,
        CancellationToken,
        Task<HttpResponseMessage>> Sequenced(
            HttpStatusCode firstStatus,
            HttpResponseMessage next)
        => Sequenced(
            exception: null,
            next: next,
            firstStatus: firstStatus);

    private static Func<
        HttpRequestMessage,
        CancellationToken,
        Task<HttpResponseMessage>> Sequenced(
            Exception exception,
            HttpResponseMessage next)
        => Sequenced(exception, next, firstStatus: null);

    private static Func<
        HttpRequestMessage,
        CancellationToken,
        Task<HttpResponseMessage>> Sequenced(
            Exception? exception,
            HttpResponseMessage next,
            HttpStatusCode? firstStatus)
    {
        var attempts = 0;

        return (_, _) =>
        {
            attempts++;

            if (attempts == 1)
            {
                if (exception is not null)
                {
                    throw exception;
                }

                return Task.FromResult(
                    new HttpResponseMessage(firstStatus!.Value)
                    {
                        Content = new StringContent("{}"),
                    });
            }

            return Task.FromResult(next);
        };
    }

    private async Task<DocumentExtractionResult> ExtractAsync(
        RecordingHttpMessageHandler handler,
        int maxRetries = 2,
        int? maxResponseBytes = null)
    {
        using var factory = Factory.WithWebHostBuilder(builder =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Extraction:Provider"] = "OpenRouter",
                ["Extraction:OpenRouter:Endpoint"] =
                    "https://openrouter.example.test/v1",
                ["Extraction:OpenRouter:Model"] = "test-model",
                ["Extraction:OpenRouter:MaxTokens"] = "512",
                ["Extraction:OpenRouter:RequestTimeoutSeconds"] = "30",
                ["Extraction:OpenRouter:MaxRetries"] =
                    maxRetries.ToString(
                        System.Globalization.CultureInfo
                            .InvariantCulture),
                ["Extraction:OpenRouter:ApiKey"] =
                    "test-only-key-not-a-real-secret",
            };

            if (maxResponseBytes.HasValue)
            {
                overrides["Extraction:OpenRouter:MaxResponseBytes"] =
                    maxResponseBytes.Value.ToString(
                        System.Globalization.CultureInfo
                            .InvariantCulture);
            }

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(overrides));

            builder.ConfigureServices(services =>
                services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
                    new ReplacePrimaryHandlerFilter(handler)));
        });

        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<OpenRouterDocumentExtractionService>();

        var pdf = TestPdfBuilder.CreateTextPdf(
            TestDocumentFixtures.PassportLines());

        return await service.ExtractAsync(
            new MemoryStream(pdf),
            "application/pdf",
            CancellationToken.None);
    }

    /// <summary>
    /// Replaces the primary handler of every typed client with the recorded
    /// fake so retry attempts can be counted without a network call.
    /// </summary>
    private sealed class ReplacePrimaryHandlerFilter
        : IHttpMessageHandlerBuilderFilter
    {
        private readonly HttpMessageHandler _handler;

        public ReplacePrimaryHandlerFilter(HttpMessageHandler handler)
            => _handler = handler;

        public Action<HttpMessageHandlerBuilder> Configure(
            Action<HttpMessageHandlerBuilder> next)
            => builder =>
            {
                next(builder);
                builder.PrimaryHandler = _handler;
            };
    }
}