using Microsoft.Extensions.Options;
using PROManagementSystem.Infrastructure.Extraction;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.1: recovers the raw document text that feeds the
/// <see cref="OpenRouterPromptBuilder"/>. Selectable PDFs are parsed for their
/// text layer; scanned PDFs (no selectable text) are rasterised and OCR'd page
/// by page; raster JPEG/PNG images are recognised with local Tesseract OCR.
/// The text stays internal to the pipeline — it is never surfaced through the
/// API, storage, or logs. Heavy OCR is protected by the process-wide
/// <see cref="OcrWorkloadGate"/> and a server-side deadline
/// (<see cref="OcrOptions.OcrTimeoutSeconds"/>); a deadline expiry degrades to
/// null while client cancellation still propagates. The OCR components
/// themselves are the same ones the Local provider uses — they are reused, not
/// modified.
/// </summary>
internal sealed class DocumentTextRecoveryService
    : IDocumentTextRecoveryService
{
    private const string PdfContentType = "application/pdf";
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IImageOcrExtractor _imageOcrExtractor;
    private readonly ScannedPdfOcrService _scannedPdfOcrService;
    private readonly OcrWorkloadGate _gate;
    private readonly OcrOptions _options;

    public DocumentTextRecoveryService(
        IPdfTextExtractor pdfTextExtractor,
        IImageOcrExtractor imageOcrExtractor,
        ScannedPdfOcrService scannedPdfOcrService,
        OcrWorkloadGate gate,
        IOptions<OcrOptions> options)
    {
        _pdfTextExtractor = pdfTextExtractor;
        _imageOcrExtractor = imageOcrExtractor;
        _scannedPdfOcrService = scannedPdfOcrService;
        _gate = gate;
        _options = options.Value;
    }

    public Task<string?> RecoverAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (contentType.Equals(
                PdfContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return RecoverPdfAsync(document, cancellationToken);
        }

        if (contentType.Equals(
                JpegContentType,
                StringComparison.OrdinalIgnoreCase)
            || contentType.Equals(
                PngContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return RecoverImageAsync(document, cancellationToken);
        }

        // Unsupported content type is handled by the upload pipeline before
        // extraction; any stream reaching this provider is a PDF or a JPEG/PNG.
        return Task.FromResult<string?>(null);
    }

    private async Task<string?> RecoverPdfAsync(
        Stream document,
        CancellationToken cancellationToken)
    {
        var textResult = await _pdfTextExtractor.ExtractAsync(
            document,
            cancellationToken);

        if (textResult.HasUsableText)
        {
            return textResult.Text;
        }

        // No selectable text layer means the document is a scan; the OCR
        // fallback rasterises the pages and recognises each page image. The
        // recovered text stays internal to the pipeline.
        var scanned = await RunHeavyOcrAsync(
            token => _scannedPdfOcrService.OcrAsync(
                document,
                token),
            cancellationToken);

        return scanned is { HasUsableText: true }
            ? scanned.CombinedText
            : null;
    }

    private async Task<string?> RecoverImageAsync(
        Stream document,
        CancellationToken cancellationToken)
    {
        var ocr = await RunHeavyOcrAsync(
            token => _imageOcrExtractor.ExtractAsync(
                document,
                token),
            cancellationToken);

        return ocr is { HasUsableText: true }
            ? ocr.Text
            : null;
    }

    /// <summary>
    /// Runs one complete heavy OCR operation (scanned-PDF rasterisation + OCR
    /// or a raster image OCR) under the shared workload gate and the server
    /// deadline — the same convention used by the Local provider. The whole
    /// operation holds exactly one gate slot; the gate is never acquired inside
    /// the extractor/renderer, which would risk a re-entrant self-deadlock. A
    /// deadline expiry (the linked timeout token cancelled while the caller's
    /// token stays live) degrades to null. A client/request cancellation is
    /// never absorbed and propagates untouched.
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