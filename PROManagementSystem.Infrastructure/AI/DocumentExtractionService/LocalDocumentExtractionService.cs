using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Extraction;

namespace PROManagementSystem.Infrastructure.AI;

/// <summary>
/// In-process extraction provider. Selectable PDFs are parsed for their text
/// layer; scanned PDFs (no selectable text) are rasterised and OCR'd page by
/// page; raster JPEG/PNG images are recognised with local Tesseract OCR.
/// Recovered text is passed to <see cref="IDocumentClassifier"/> so the
/// suggested type and classification confidence are returned alongside the
/// neutral fields; it is also passed to <see cref="IDocumentDetailsExtractor"/>
/// (Task 7B.5) so schema-defined field suggestions are returned for
/// <see cref="Entities.DocumentExtraction.SuggestedDetails"/>. The text itself
/// is never surfaced through the API, storage, or logs. Heavy OCR is protected
/// by the process-wide <see cref="OcrWorkloadGate"/> (capped concurrency) and
/// a server-side deadline (<see cref="OcrOptions.OcrTimeoutSeconds"/>); a
/// deadline expiry degrades to a neutral result while client cancellation
/// still propagates.
/// </summary>
public sealed class LocalDocumentExtractionService
    : IDocumentExtractionService
{
    private const string PdfContentType = "application/pdf";
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IImageOcrExtractor _imageOcrExtractor;
    private readonly ScannedPdfOcrService _scannedPdfOcrService;
    private readonly IDocumentClassifier _classifier;
    private readonly IDocumentDetailsExtractor _detailsExtractor;
    private readonly OcrWorkloadGate _gate;
    private readonly OcrOptions _options;

    public LocalDocumentExtractionService(
        IPdfTextExtractor pdfTextExtractor,
        IImageOcrExtractor imageOcrExtractor,
        ScannedPdfOcrService scannedPdfOcrService,
        IDocumentClassifier classifier,
        IDocumentDetailsExtractor detailsExtractor,
        OcrWorkloadGate gate,
        IOptions<OcrOptions> options)
    {
        _pdfTextExtractor = pdfTextExtractor;
        _imageOcrExtractor = imageOcrExtractor;
        _scannedPdfOcrService = scannedPdfOcrService;
        _classifier = classifier;
        _detailsExtractor = detailsExtractor;
        _gate = gate;
        _options = options.Value;
    }

    public async Task<DocumentExtractionResult> ExtractAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken)
    {
        string? recoveredText = null;

        if (contentType.Equals(
                PdfContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            var textResult = await _pdfTextExtractor.ExtractAsync(
                document,
                cancellationToken);

            if (textResult.HasUsableText)
            {
                recoveredText = textResult.Text;
            }
            else
            {
                // No selectable text layer means the document is a scan; the
                // OCR fallback rasterises the pages and recognises each page
                // image. The recovered text stays internal to the pipeline.
                var scanned = await RunHeavyOcrAsync(
                    token => _scannedPdfOcrService.OcrAsync(
                        document,
                        token),
                    cancellationToken);

                if (scanned is { HasUsableText: true })
                {
                    recoveredText = scanned.CombinedText;
                }
            }
        }
        else if (contentType.Equals(
                     JpegContentType,
                     StringComparison.OrdinalIgnoreCase)
                 || contentType.Equals(
                     PngContentType,
                     StringComparison.OrdinalIgnoreCase))
        {
            var ocr = await RunHeavyOcrAsync(
                token => _imageOcrExtractor.ExtractAsync(
                    document,
                    token),
                cancellationToken);

            if (ocr is { HasUsableText: true })
            {
                recoveredText = ocr.Text;
            }
        }

        DocumentType? suggestedType = null;
        decimal confidence = 0m;
        IReadOnlyDictionary<string, object?>? details = null;

        if (!string.IsNullOrWhiteSpace(recoveredText))
        {
            var classification = await _classifier.ClassifyAsync(
                recoveredText,
                cancellationToken);

            suggestedType = classification.Type;
            confidence = classification.Confidence;

            // Task 7B.5: schema-driven, deterministic field extraction for the
            // suggested type. The result is a non-authoritative suggestion only;
            // it never becomes Document.Details and no extraction confidence is
            // produced. An empty or unknown-type result is safe (persists "{}").
            if (suggestedType.HasValue)
            {
                var extraction = await _detailsExtractor.ExtractAsync(
                    recoveredText,
                    suggestedType,
                    cancellationToken);

                if (extraction.Details.Count > 0)
                {
                    details = extraction.Details;
                }
            }
        }

        return new DocumentExtractionResult(
            suggestedType,
            null,
            null,
            null,
            null,
            confidence)
        {
            Details = details
        };
    }

    /// <summary>
    /// Runs one complete heavy OCR operation (scanned-PDF rasterisation + OCR
    /// or a raster image OCR) under the shared workload gate and the server
    /// deadline. The whole operation holds exactly one gate slot — the gate is
    /// never acquired inside the extractor/renderer, which would risk a
    /// re-entrant self-deadlock. A deadline expiry (the linked timeout token
    /// cancelled while the caller's token stays live) is treated as OCR
    /// degradation: default/null is returned and the stored file is preserved.
    /// A client/request cancellation is never absorbed and propagates
    /// untouched.
    /// </summary>
    private async Task<T?> RunHeavyOcrAsync<T>(
        Func<CancellationToken, Task<T>> ocrOperation,
        CancellationToken callerToken)
    {
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(callerToken);

        if (_options.OcrTimeoutSeconds > 0)
        {
            linkedCts.CancelAfter(
                TimeSpan.FromSeconds(_options.OcrTimeoutSeconds));
        }

        var token = linkedCts.Token;

        try
        {
            await _gate.WaitAsync(token);
        }
        catch (OperationCanceledException)
            when (!callerToken.IsCancellationRequested)
        {
            // Our server deadline expired while queued for a gate slot; the
            // OCR never started and no slot was acquired.
            return default;
        }

        try
        {
            try
            {
                return await ocrOperation(token);
            }
            catch (OperationCanceledException)
                when (!callerToken.IsCancellationRequested)
            {
                // Server-side OCR deadline fired. Treat the slow OCR as
                // degradation (a neutral draft) rather than a request failure.
                return default;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}