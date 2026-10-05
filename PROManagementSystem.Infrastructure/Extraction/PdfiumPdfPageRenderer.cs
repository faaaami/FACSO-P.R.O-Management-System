using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Microsoft.Extensions.Options;
using PDFtoImage;
using PDFtoImage.Exceptions;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// PDFium-backed page renderer for scanned PDFs (Task 6B.5). Pages are
/// rasterised at <see cref="OcrOptions.RenderDpi"/> and clamped to
/// <see cref="OcrOptions.MaximumPixels"/> so a single page can never blow up
/// into an unbounded bitmap. The caller-owned stream is never disposed nor
/// repositioned; the input bytes are buffered once per stream so multi-page
/// documents are read a single time.
/// </summary>
[SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification =
        "PDFtoImage wraps native PDFium/SkiaSharp binaries that are deployed "
        + "for the supported server platforms (Windows and Linux containers); "
        + "this renderer never runs on the unsupported client platforms.")]
public sealed class PdfiumPdfPageRenderer : IPdfPageRenderer
{
    private const double PointsPerInch = 72d;

    private readonly OcrOptions _options;
    private Stream? _cachedStream;
    private byte[]? _cachedBytes;

    public PdfiumPdfPageRenderer(IOptions<OcrOptions> options)
    {
        _options = options.Value;
    }

    public Task<int> GetPageCountAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableStream(pdfStream, nameof(pdfStream));

        return Task.Run(
            () => CountPages(pdfStream, cancellationToken),
            cancellationToken);
    }

    public Task<PdfPageRenderResult> RenderPageAsync(
        Stream pdfStream,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableStream(pdfStream, nameof(pdfStream));

        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "Page numbers are one-based.");
        }

        return Task.Run(
            () => RenderPage(pdfStream, pageNumber, cancellationToken),
            cancellationToken);
    }

    private static void EnsureReadableStream(
        Stream stream,
        string parameterName)
    {
        if (stream is null || !stream.CanRead)
        {
            throw new ArgumentException(
                "The PDF stream must be readable.",
                parameterName);
        }
    }

    private int CountPages(
        Stream pdfStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = GetBytes(pdfStream);

        return Conversion.GetPageCount(bytes);
    }

    private PdfPageRenderResult RenderPage(
        Stream pdfStream,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = GetBytes(pdfStream);

        var pageIndex = new Index(pageNumber - 1);

        SizeF size;

        try
        {
            size = Conversion.GetPageSize(bytes, pageIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new PdfPageNotFoundException();
        }

        var scale = ComputeScale(size.Width, size.Height);

        var targetWidth = Math.Max(1, (int)Math.Floor(size.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Floor(size.Height * scale));

        var output = new MemoryStream();

        try
        {
            Conversion.SavePng(
                output,
                bytes,
                pageIndex,
                password: null,
                new RenderOptions
                {
                    Width = targetWidth,
                    Height = targetHeight
                });
        }
        catch (ArgumentOutOfRangeException)
        {
            output.Dispose();
            throw new PdfPageNotFoundException();
        }

        output.Position = 0;

        return new PdfPageRenderResult(
            output,
            targetWidth,
            targetHeight);
    }

    /// <summary>
    /// The requested DPI expressed as a scale factor, capped so the rendered
    /// page stays within <see cref="OcrOptions.MaximumPixels"/> (the final
    /// safety net still sits in the OCR extractor, which measures the decoded
    /// bitmap directly).
    /// </summary>
    private double ComputeScale(float pageWidth, float pageHeight)
    {
        var scale = _options.RenderDpi / PointsPerInch;

        if (_options.MaximumPixels > 0 && pageWidth > 0 && pageHeight > 0)
        {
            var pixelCapScale = Math.Sqrt(
                _options.MaximumPixels / ((double)pageWidth * pageHeight));

            scale = Math.Min(scale, pixelCapScale);
        }

        return scale;
    }

    /// <summary>
    /// Buffers the whole PDF once per input stream; subsequent page renders
    /// reuse the cached bytes so a scan of many pages is only read from disk
    /// a single time.
    /// </summary>
    private byte[] GetBytes(Stream pdfStream)
    {
        if (ReferenceEquals(_cachedStream, pdfStream)
            && _cachedBytes is not null)
        {
            return _cachedBytes;
        }

        var originalPosition = pdfStream.CanSeek ? pdfStream.Position : (long?)null;

        try
        {
            using var buffer = new MemoryStream();

            pdfStream.CopyTo(buffer);

            var bytes = buffer.ToArray();

            _cachedStream = pdfStream;
            _cachedBytes = bytes;

            return bytes;
        }
        finally
        {
            if (originalPosition is not null)
            {
                pdfStream.Position = originalPosition.Value;
            }
        }
    }
}