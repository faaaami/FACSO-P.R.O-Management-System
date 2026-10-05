namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// OpenRouter provider configuration (Task 7C.1). This is configuration
/// foundation only: no HTTP client, prompts, or AI extraction logic exists
/// yet. The options are bound from the "Extraction:OpenRouter" section and
/// only take effect once a later task registers an OpenRouter-backed
/// <see cref="PROManagementSystem.Application.Common.Interfaces.IDocumentExtractionService"/>.
///
/// Non-secret values carry safe defaults so the section can be absent from
/// configuration entirely. The ApiKey is always empty by default and is
/// expected from per-machine sources (appsettings.Local.json or the
/// Extraction__OpenRouter__ApiKey environment variable); the application must
/// keep booting with the default Mock provider and no API key.
/// </summary>
public sealed class OpenRouterOptions
{
    /// <summary>OpenRouter API key. Never committed; empty until supplied.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenRouter API endpoint. Left empty so the choice is explicit in
    /// configuration rather than assumed by code.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier. Left empty until an explicit model choice is made;
    /// empty must not affect the Mock/Local providers.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Maximum completion tokens allowed per request.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Server-side request deadline in seconds, matching the OcrOptions
    /// deadline convention.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>Maximum retries for retryable HTTP failures.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Task 7C.3.4: maximum number of recovered document characters sent to
    /// OpenRouter in a single request. A pathological document (for example a
    /// very long selectable-text PDF) could otherwise produce an unbounded
    /// prompt and unbounded model cost. When the recovered text exceeds this
    /// limit it is deterministically truncated to preserve the beginning and a
    /// short truncation notice is appended, so the model is told the text was
    /// cut off. A value of 0 or less falls back to the built-in conservative
    /// default rather than disabling the bound.
    /// </summary>
    public int MaxInputCharacters { get; set; } = 20_000;

    /// <summary>
    /// Task 7C.3.4: maximum number of bytes of the OpenRouter HTTP response
    /// body that will be read. A malicious or broken upstream must not cause
    /// unbounded memory growth; a response larger than this is discarded as a
    /// controlled fail-soft failure (neutral result, never retried). A value of
    /// 0 or less falls back to the built-in conservative default rather than
    /// disabling the bound.
    /// </summary>
    public int MaxResponseBytes { get; set; } = 1_048_576;
}