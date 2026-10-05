using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.1: the OpenRouter extraction provider HTTP foundation. It
/// recovers the document text through the existing OCR/PDF pipeline
/// (<see cref="IDocumentTextRecoveryService"/>), classifies it with the
/// existing deterministic classifier, builds the prompt through
/// <see cref="OpenRouterPromptBuilder"/> (which reuses the schema provider),
/// and POSTs an OpenRouter chat/completions-compatible request to the
/// configured endpoint. Each successful 2xx response reaches the internal
/// raw-response boundary (<see cref="OpenRouterRawResponse"/>) for the
/// structured parser (Task 7C.3.2); today the placeholder parser returns a
/// neutral result so the extraction pipeline stays fail-soft.
///
/// Fail-soft contract: transport failures (429, 5xx, other 4xx, network
/// errors, timeouts, empty bodies) never fail the request — the provider logs
/// a controlled warning (status only) and returns a neutral result so the
/// stored file remains and the DocumentExtraction can still become a Pending
/// draft with neutral suggestions. Caller cancellation is never swallowed and
/// propagates untouched. Configuration errors surface before any HTTP call.
///
/// Security/privacy boundary: the request body carries only model, prompts,
/// and max_tokens. It never contains user/client/employee/extraction IDs, file
/// paths, storage URLs, database data, or API keys. The API key travels only
/// in the Authorization bearer header and is never logged. Neither the
/// document text nor the raw model response is ever logged, stored, or
/// surfaced through the API.
/// </summary>
public sealed class OpenRouterDocumentExtractionService
    : IDocumentExtractionService, IDisposable
{
    private const string ChatCompletionsPath = "chat/completions";

    // Task 7C.3.4 fallback. It is only used when MaxInputCharacters is 0 or
    // negative, so a misconfiguration can never disable the bound.
    private const int DefaultMaxInputCharacters = 20_000;

    // Appended after a deterministic truncation so the model is explicitly
    // told the recovered text was cut off; the notice is part of the bounded
    // payload, never an addition on top of the limit.
    private const string TruncationNotice = "\n\n[document text truncated]";

    private static readonly DocumentExtractionResult NeutralResult =
        new(null, null, null, null, null, 0m);

    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly OpenRouterPromptBuilder _promptBuilder;
    private readonly IDocumentClassifier _classifier;
    private readonly IDocumentTextRecoveryService _textRecovery;
    private readonly IOpenRouterResponseParser _parser;
    private readonly ILogger<OpenRouterDocumentExtractionService> _logger;

    // The constructor is internal so the provider's seams (text recovery and
    // response parsing) stay internal until 7C.3.2. DI instantiates it through
    // the explicit factory in OpenRouterExtractionServiceExtensions (which owns
    // the named HttpClient); the test project discovers it via the
    // InternalsVisibleTo seam.
    internal OpenRouterDocumentExtractionService(
        HttpClient httpClient,
        IOptions<OpenRouterOptions> options,
        OpenRouterPromptBuilder promptBuilder,
        IDocumentClassifier classifier,
        IDocumentTextRecoveryService textRecovery,
        IOpenRouterResponseParser parser,
        ILogger<OpenRouterDocumentExtractionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _classifier = classifier;
        _textRecovery = textRecovery;
        _parser = parser;
        _logger = logger;
    }

    public async Task<DocumentExtractionResult> ExtractAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken)
    {
        var recoveredText = await _textRecovery.RecoverAsync(
            document,
            contentType,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(recoveredText))
        {
            // No usable text means nothing to send to the model; degrade to a
            // neutral draft rather than failing the request.
            return NeutralResult;
        }

        // Task 7C.3.4: bound the payload before classification and prompt
        // building so a pathological document cannot produce an unbounded
        // request (and unbounded cost). Truncation is deterministic and the
        // model is told the text was cut off.
        var boundedText = BoundInput(recoveredText);

        var classification = await _classifier.ClassifyAsync(
            boundedText,
            cancellationToken);

        var prompt = _promptBuilder.Build(
            boundedText,
            classification.Type);

        OpenRouterRawResponse raw;

        try
        {
            raw = await SendRawAsync(prompt, cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout (RequestTimeoutSeconds) fired, not the caller.
            // Treat the slow model response as a controlled degradation.
            _logger.LogWarning(
                "OpenRouter extraction timed out after {TimeoutSeconds}s.",
                _options.RequestTimeoutSeconds);
            return NeutralResult;
        }
        catch (HttpRequestException ex)
        {
            // Network/transport failure, or the upstream body exceeded
            // MaxResponseContentBufferSize. Log only the transport-level
            // message (never the request body or any response body) and
            // degrade. Because HttpClient buffers the body after the
            // resilience pipeline has returned, an oversized body is not
            // retried (no repeated paid model calls).
            _logger.LogWarning(
                ex,
                "OpenRouter extraction failed with a transport error.");
            return NeutralResult;
        }

        if (!raw.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenRouter extraction failed: statusCode={StatusCode}.",
                (int)raw.StatusCode);
            return NeutralResult;
        }

        if (string.IsNullOrWhiteSpace(raw.JsonContent))
        {
            _logger.LogWarning(
                "OpenRouter extraction returned an empty response: " +
                "statusCode={StatusCode}.",
                (int)raw.StatusCode);
            return NeutralResult;
        }

        // 2xx raw response reaches the internal parsing boundary. The
        // placeholder parser keeps the suggestion neutral until 7C.3.2.
        return _parser.Parse(
            raw,
            classification.Type,
            classification.Confidence);
    }

    /// <summary>
    /// The HTTP transport. Builds and dispatches the chat/completions POST and
    /// returns the raw response to the internal boundary. Does not log the
    /// request body, the response body, or the API key, and never aborts on a
    /// caller cancellation (it propagates). Retry semantics are applied by the
    /// DI-resilience pipeline configured around the named <see cref="HttpClient"/>;
    /// this method itself performs exactly one attempt.
    /// </summary>
    internal async Task<OpenRouterRawResponse> SendRawAsync(
        OpenRouterPrompt prompt,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                model = _options.Model,
                messages = new object[]
                {
                    new { role = "system", content = prompt.SystemPrompt },
                    new { role = "user", content = prompt.UserPrompt },
                },
                // Task 7C.3.4: a zero/negative MaxTokens must never produce an
                // invalid request; clamp to the smallest valid completion
                // budget rather than sending an out-of-range value.
                max_tokens = Math.Max(1, _options.MaxTokens),
            });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            ChatCompletionsPath)
        {
            Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.ApiKey);

        // ResponseContentRead (the default) keeps the whole exchange -
        // including the response body - under HttpClient.Timeout, and lets
        // HttpClient enforce MaxResponseContentBufferSize (set on the named
        // client) so an oversized upstream body is bounded. HttpClient buffers
        // the body after the resilience pipeline returns, so an oversized body
        // is a controlled degradation rather than a retry trigger.
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return new OpenRouterRawResponse(
            response.StatusCode,
            response.IsSuccessStatusCode,
            json);
    }

    /// <summary>
    /// Task 7C.3.4: deterministically truncates the recovered document text to
    /// the configured character limit, preserving its beginning and appending a
    /// short truncation notice so the model knows the text was cut off. The
    /// returned payload never exceeds the effective limit (the notice is
    /// reserved inside it). A non-positive limit falls back to the built-in
    /// default instead of disabling the bound.
    /// </summary>
    private string BoundInput(string text)
    {
        var limit = _options.MaxInputCharacters > 0
            ? _options.MaxInputCharacters
            : DefaultMaxInputCharacters;

        if (text.Length <= limit)
        {
            return text;
        }

        var keep = Math.Max(0, limit - TruncationNotice.Length);

        return string.Concat(text.AsSpan(0, keep), TruncationNotice);
    }

    /// <summary>
    /// Disposes the factory-created named HttpClient wrapper. Disposing this
    /// wrapper does not tear down the factory's pooled handlers.
    /// </summary>
    public void Dispose() => _httpClient.Dispose();
}