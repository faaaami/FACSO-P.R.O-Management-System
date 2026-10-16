using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using System.Globalization;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Extraction;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// The optional AI step of the extraction pipeline (Extraction:Ai:Enabled).
/// Recognised text and the staff-selected document type are turned into a
/// prompt by <see cref="OpenRouterPromptBuilder"/>, POSTed to an OpenRouter
/// chat/completions endpoint, and the reply is parsed by
/// <see cref="OpenRouterResponseParser"/> into schema-validated values.
///
/// Fail-soft: transport failures (429, 5xx, other 4xx, network errors,
/// timeouts, empty bodies) return <see cref="AiFieldExtraction.Unavailable"/>
/// and log only the status or timeout, never text, values or the API key.
/// Caller cancellation is never swallowed. Retries are configured on the
/// HttpClient (see <see cref="OpenRouterExtractionServiceExtensions"/>).
/// </summary>
public sealed class OpenRouterDocumentExtractionService
    : IAiFieldExtractor, IDisposable
{
    private const string ChatCompletionsPath = "chat/completions";

    private const int DefaultMaxInputCharacters = 20_000;

    private const string TruncationNotice = "\n\n[document text truncated]";

    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly OpenRouterPromptBuilder _promptBuilder;
    private readonly IOpenRouterResponseParser _parser;
    private readonly ILogger<OpenRouterDocumentExtractionService> _logger;

    internal OpenRouterDocumentExtractionService(
        HttpClient httpClient,
        IOptions<OpenRouterOptions> options,
        OpenRouterPromptBuilder promptBuilder,
        IOpenRouterResponseParser parser,
        ILogger<OpenRouterDocumentExtractionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Sends recognised text for the staff-selected type to the configured
    /// model and returns the values it found. Never throws for service
    /// failures: timeouts, transport errors, non-success statuses and
    /// unusable responses return <see cref="AiFieldExtraction.Unavailable"/>.
    /// Only the status code or timeout is logged, never text or values.
    /// </summary>
    public async Task<AiFieldExtraction> ExtractAsync(
        string documentText,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return new AiFieldExtraction(true, new Dictionary<string, string>());
        }

        var prompt = _promptBuilder.Build(BoundInput(documentText), documentType);

        OpenRouterRawResponse raw;

        try
        {
            raw = await SendRawAsync(prompt, cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "OpenRouter extraction timed out after {TimeoutSeconds}s.",
                _options.RequestTimeoutSeconds);

            return AiFieldExtraction.Unavailable;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "OpenRouter extraction failed with a transport error.");

            return AiFieldExtraction.Unavailable;
        }

        if (!raw.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw.JsonContent))
        {
            _logger.LogWarning(
                "OpenRouter extraction failed: statusCode={StatusCode}.",
                (int)raw.StatusCode);

            return AiFieldExtraction.Unavailable;
        }

        var parsed = _parser.Parse(raw, documentType, 0m);

        return new AiFieldExtraction(true, ToValues(parsed));
    }

    private static IReadOnlyDictionary<string, string> ToValues(AiParsedExtraction parsed)
    {
        var values = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(parsed.DocumentNumber))
        {
            values["documentNumber"] = parsed.DocumentNumber;
        }

        if (parsed.IssueDate is { } issue)
        {
            values["issueDate"] = issue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (parsed.ExpiryDate is { } expiry)
        {
            values["expiryDate"] = expiry.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        foreach (var (key, value) in parsed.Details ?? new Dictionary<string, object?>())
        {
            var text = value switch
            {
                null => null,
                string s => s,
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                values[key] = text;
            }
        }

        return values;
    }

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