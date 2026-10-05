using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.1: DI wiring for the OpenRouter extraction provider. Living in
/// Infrastructure keeps the provider's internal seams (text recovery and the
/// raw-response parsing boundary) private to this assembly — the API project
/// only hands over the already-read <see cref="OpenRouterOptions"/>.
///
/// A NAMED HttpClient is used rather than a typed one because the provider's
/// constructor is internal (its seams are internal, pending 7C.3.2); the typed
/// client factory only accepts public constructors. The named client follows
/// the project's existing HttpClient convention (see Razorpay) and carries an
/// explicit, cautious resilience policy: retry only clearly retryable failures
/// (network/transport errors, HTTP 429, HTTP 5xx), never an ordinary 4xx, so a
/// single model call is not blindly re-fired and charged more than once.
/// HttpClient.Timeout is the sole request deadline, sourced from
/// RequestTimeoutSeconds — no second timeout is invented.
/// </summary>
public static class OpenRouterExtractionServiceExtensions
{
    /// <summary>The named HttpClient used by the OpenRouter provider.</summary>
    public const string HttpClientName = "extraction-openrouter";

    // Task 7C.3.4: fallback response-size bound, used only when
    // MaxResponseBytes is 0 or negative so a misconfiguration cannot disable
    // the bound. Mirrors the OpenRouterOptions default.
    private const long DefaultMaxResponseBytes = 1_048_576;

    public static IServiceCollection AddOpenRouterExtractionProvider(
        this IServiceCollection services)
    {
        services.AddSingleton<OpenRouterPromptBuilder>();
        services.AddScoped<
            IDocumentTextRecoveryService,
            DocumentTextRecoveryService>();
        services.AddScoped<
            IOpenRouterResponseParser,
            OpenRouterResponseParser>();

        // The endpoint and timeout are read from IOptions at client-creation
        // time (not from a startup snapshot) so late configuration sources -
        // environment overrides, test hosts - are honored by the transport.
        services.AddHttpClient(HttpClientName, (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<IOptions<OpenRouterOptions>>()
                    .Value;

                // The endpoint is configuration-controlled; the code never
                // assumes a production host. The trailing slash makes the
                // relative "chat/completions" path resolve under the
                // configured base root.
                if (!string.IsNullOrWhiteSpace(settings.Endpoint))
                {
                    client.BaseAddress = new Uri(
                        settings.Endpoint.TrimEnd('/') + "/");
                }

                if (settings.RequestTimeoutSeconds > 0)
                {
                    client.Timeout = TimeSpan.FromSeconds(
                        settings.RequestTimeoutSeconds);
                }

                // Task 7C.3.4: bound the buffered response size. HttpClient
                // enforces this while buffering the body (after the resilience
                // pipeline has returned), so an oversized upstream body raises
                // a transport exception that degrades neutrally instead of
                // triggering another paid retry. A non-positive value falls
                // back to the built-in default rather than disabling the bound.
                client.MaxResponseContentBufferSize =
                    settings.MaxResponseBytes > 0
                        ? settings.MaxResponseBytes
                        : DefaultMaxResponseBytes;
            })
            .AddResilienceHandler(
                "extraction-openrouter-retry",
                (pipeline, context) =>
                {
                    // Task 7C.3.4: the retry budget is read from IOptions when
                    // the handler chain is built (not from a startup snapshot),
                    // matching how the endpoint and timeout are resolved. This
                    // keeps MaxRetries authoritative at runtime and prevents an
                    // unbounded or stale retry budget.
                    var settings = context.ServiceProvider
                        .GetRequiredService<IOptions<OpenRouterOptions>>()
                        .Value;

                    // MaxRetries = 0 means "no retries" (exactly one attempt).
                    // The retry strategy requires MaxRetryAttempts >= 1, so a
                    // zero budget must omit the strategy entirely rather than
                    // register an invalid one (which would break the client).
                    var maxRetries = Math.Max(0, settings.MaxRetries);

                    if (maxRetries == 0)
                    {
                        return;
                    }

                    pipeline.AddRetry(
                        new RetryStrategyOptions<HttpResponseMessage>
                        {
                            MaxRetryAttempts = maxRetries,
                            BackoffType = DelayBackoffType.Exponential,
                            Delay = TimeSpan.FromMilliseconds(500),
                            MaxDelay = TimeSpan.FromSeconds(5),
                            ShouldHandle = args => ValueTask.FromResult(
                                args.Outcome.Exception is HttpRequestException
                                || (args.Outcome.Result is { } result
                                    && (result.StatusCode
                                            == HttpStatusCode.TooManyRequests
                                        || (int)result.StatusCode >= 500)))
                        });
                });

        services.AddScoped<OpenRouterDocumentExtractionService>(
            serviceProvider =>
            {
                var httpClient = serviceProvider
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient(HttpClientName);

                return new OpenRouterDocumentExtractionService(
                    httpClient,
                    serviceProvider.GetRequiredService<IOptions<OpenRouterOptions>>(),
                    serviceProvider.GetRequiredService<OpenRouterPromptBuilder>(),
                    serviceProvider.GetRequiredService<IDocumentClassifier>(),
                    serviceProvider.GetRequiredService<IDocumentTextRecoveryService>(),
                    serviceProvider.GetRequiredService<IOpenRouterResponseParser>(),
                    serviceProvider.GetRequiredService<ILogger<OpenRouterDocumentExtractionService>>());
            });

        return services;
    }
}