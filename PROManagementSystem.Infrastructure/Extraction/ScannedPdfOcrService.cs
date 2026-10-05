using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage.Exceptions;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// The text recognised from all pages of a scanned PDF.
/// <see cref="HasUsableText"/> is <see langword="true"/> only when at least one
/// rendered page produced non-whitespace text.
/// </summary>
public sealed record ScannedPdfOcrResult(
    string CombinedText,
    bool HasUsableText);

/// <summary>
/// Rasterises every page of a scanned PDF through
/// <see cref="IPdfPageRenderer"/> and recognises each page's image with the
/// shared local OCR extractor. The combined text stays internal to the
/// extraction pipeline. PDFs whose page count exceeds
/// <see cref="OcrOptions.MaximumOcrPages"/> are skipped entirely so a single
/// oversized scan cannot exhaust memory; the page-count check happens before
/// any page is rendered. Documents with no selectable text therefore resolve
/// to a neutral draft instead of failing the request.
/// </summary>
public sealed class ScannedPdfOcrService
{
    private readonly IPdfPageRenderer _pageRenderer;
    private readonly IImageOcrExtractor _imageOcrExtractor;
    private readonly OcrOptions _options;
    private readonly ILogger<ScannedPdfOcrService> _logger;

    public ScannedPdfOcrService(
        IPdfPageRenderer pageRenderer,
        IImageOcrExtractor imageOcrExtractor,
        IOptions<OcrOptions> options,
        ILogger<ScannedPdfOcrService> logger)
    {
        _pageRenderer = pageRenderer;
        _imageOcrExtractor = imageOcrExtractor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScannedPdfOcrResult> OcrAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pdfStream is null || !pdfStream.CanRead)
        {
            return new ScannedPdfOcrResult(string.Empty, false);
        }

        int pageCount;

        try
        {
            pageCount = await _pageRenderer.GetPageCountAsync(
                pdfStream,
                cancellationToken);
        }
        catch (PdfException ex)
        {
            _logger.LogDebug(
                "Scanned PDF could not be opened for OCR: {Category}",
                ex.GetType().Name);

            return new ScannedPdfOcrResult(string.Empty, false);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(
                "Scanned PDF stream could not be read for OCR: {Category}",
                ex.GetType().Name);

            return new ScannedPdfOcrResult(string.Empty, false);
        }

        if (pageCount <= 0)
        {
            return new ScannedPdfOcrResult(string.Empty, false);
        }

        if (_options.MaximumOcrPages > 0
            && pageCount > _options.MaximumOcrPages)
        {
            _logger.LogWarning(
                "Scanned PDF has {PageCount} pages which exceeds "
                + "MaximumOcrPages {MaximumOcrPages}; OCR skipped.",
                pageCount,
                _options.MaximumOcrPages);

            return new ScannedPdfOcrResult(string.Empty, false);
        }

        var pageTexts = new List<string>(pageCount);

        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PdfPageRenderResult rendered;

            try
            {
                rendered = await _pageRenderer.RenderPageAsync(
                    pdfStream,
                    pageNumber,
                    cancellationToken);
            }
            catch (PdfException ex)
            {
                _logger.LogDebug(
                    "Scanned PDF page {PageNumber} could not be rendered: "
                    + "{Category}",
                    pageNumber,
                    ex.GetType().Name);

                continue;
            }
            catch (IOException ex)
            {
                _logger.LogDebug(
                    "Scanned PDF page {PageNumber} could not be read: "
                    + "{Category}",
                    pageNumber,
                    ex.GetType().Name);

                continue;
            }

            using (rendered.ImageStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                OcrTextExtractionResult ocr;

                try
                {
                    ocr = await _imageOcrExtractor.ExtractAsync(
                        rendered.ImageStream,
                        cancellationToken);
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(
                        "Scanned PDF page {PageNumber} could not be OCR'd: "
                        + "{Category}",
                        pageNumber,
                        ex.GetType().Name);

                    continue;
                }

                if (ocr.HasUsableText)
                {
                    pageTexts.Add(ocr.Text);
                }
            }
        }

        if (pageTexts.Count == 0)
        {
            return new ScannedPdfOcrResult(string.Empty, false);
        }

        return new ScannedPdfOcrResult(
            string.Join(Environment.NewLine, pageTexts),
            HasUsableText: true);
    }
}