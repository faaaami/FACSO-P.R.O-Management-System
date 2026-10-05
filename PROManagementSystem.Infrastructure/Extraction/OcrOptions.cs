namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Document extraction provider configuration. The "Local" provider runs the
/// in-process extractor that reads the text layer of selectable PDFs
/// (Task 6B.3), recognises raster JPEG/PNG images (Task 6B.4) and rasterises
/// scanned PDFs through local OCR (Task 6B.5); the default remains "Mock" so
/// existing extraction behavior is untouched under the current configuration.
/// </summary>
public sealed class OcrOptions
{
    public const string ProviderMock = "Mock";

    public const string ProviderLocal = "Local";

    /// <summary>
    /// The Task 7C.3.1 OpenRouter extraction provider: recovers document text,
    /// builds an OpenRouter chat-completions request through
    /// <see cref="PROManagementSystem.Infrastructure.AI.DocumentExtractionService.OpenRouterPromptBuilder"/>
    /// and calls the configured endpoint. Selecting it requires complete
    /// "Extraction:OpenRouter" configuration (Endpoint, Model, ApiKey); an
    /// incomplete selection fails with a clear configuration exception rather
    /// than silently falling back to another provider.
    /// </summary>
    public const string ProviderOpenRouter = "OpenRouter";

    public const string DefaultProvider = ProviderMock;

    /// <summary>Extraction:Provider value from configuration.</summary>
    public string Provider { get; set; } = DefaultProvider;

    /// <summary>
    /// Tesseract language code used for image and scanned-PDF OCR.
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// Maximum decoded pixel area accepted by the OCR extractor. Larger
    /// images resolve to an empty result as the final safety net after the
    /// scanned-PDF renderer clamps its output. A value of 0 disables the gate.
    /// </summary>
    public long MaximumPixels { get; set; } = 40_000_000;

    /// <summary>
    /// Scanned PDFs with more pages than this are skipped entirely (the whole
    /// document resolves to a neutral draft) so a single oversized scan cannot
    /// exhaust memory or OCR time. A value of 0 disables the limit.
    /// </summary>
    public int MaximumOcrPages { get; set; } = 20;

    /// <summary>
    /// Rasterisation resolution for scanned-PDF pages, in DPI.
    /// </summary>
    public int RenderDpi { get; set; } = 300;

    /// <summary>
    /// Maximum number of simultaneous heavy OCR operations (image Tesseract
    /// recognition and scanned-PDF rasterisation + page OCR) for the Local
    /// provider. The process-wide <see cref="OcrWorkloadGate"/> enforces the
    /// cap; additional requests queue for a slot. A value of 0 disables the
    /// gate so OCR runs unthrottled.
    /// </summary>
    public int MaxConcurrentOcr { get; set; } = 2;

    /// <summary>
    /// Server-side OCR deadline in seconds. Heavy OCR that cannot finish
    /// within the deadline degrades to a neutral result instead of failing the
    /// request (the stored file is preserved and the extraction can still
    /// become a Pending draft). A value of 0 disables the timeout.
    /// </summary>
    public int OcrTimeoutSeconds { get; set; } = 60;
}