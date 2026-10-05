using Microsoft.Extensions.Options;
using PDFtoImage.Exceptions;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.5: unit tests for the PDFium-backed <see cref="PdfiumPdfPageRenderer"/>.
/// Real PDFium/SkiaSharp natives are exercised against scanned-PDF fixtures
/// (embedded JPEG XObjects); the suite pins page counting, the one-based page
/// contract, PNG output, the pixel cap, the caller-stream safety contract
/// (never disposed, never repositioned, non-seekable inputs buffered) and
/// cancellation.
/// </summary>
public class PdfiumPdfPageRendererTests
{
    private const string ScanMarker = "6B5-RENDERER-MARKER";

    private readonly PdfiumPdfPageRenderer _sut = new(
        Options.Create(new OcrOptions()));

    // ------------------------------------------------------------------
    // Page counting
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A scanned one-page PDF reports a single page")]
    public async Task SinglePage_ReportsOnePage()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        Assert.Equal(1, await _sut.GetPageCountAsync(pdf));
    }

    [Fact(DisplayName =
        "A scanned three-page PDF reports three pages")]
    public async Task MultiPage_ReportsPageCount()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker),
                TestImageBuilder.CreateBlankJpeg(),
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        Assert.Equal(3, await _sut.GetPageCountAsync(pdf));
    }

    [Fact(DisplayName =
        "A selectable text PDF reports its page count too")]
    public async Task SelectablePdf_ReportsPageCount()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateTextPdf("one", "two", "three"));

        Assert.Equal(3, await _sut.GetPageCountAsync(pdf));
    }

    [Fact(DisplayName =
        "An empty page tree reports zero pages")]
    public async Task EmptyPageTree_ReportsZeroPages()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateEmptyPageTreePdf());

        Assert.Equal(0, await _sut.GetPageCountAsync(pdf));
    }

    [Fact(DisplayName =
        "Malformed bytes throw the declared PDF exception category")]
    public async Task MalformedPdf_ThrowsPdfException()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateMalformedPdf());

        await Assert.ThrowsAnyAsync<PdfException>(
            () => _sut.GetPageCountAsync(pdf));
    }

    // ------------------------------------------------------------------
    // Rendering
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A scanned page renders to a PNG stream at the requested DPI")]
    public async Task RenderPage_ProducesExpectedPngDimensions()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        var result = await _sut.RenderPageAsync(pdf, 1);

        try
        {
            Assert.True(HasPngMagic(result.ImageStream));
            Assert.Equal(0, result.ImageStream.Position);
            Assert.Equal(2550, result.Width);
            Assert.Equal(3300, result.Height);
        }
        finally
        {
            result.ImageStream.Dispose();
        }
    }

    [Fact(DisplayName =
        "The rendered page is clamped to the MaximumPixels cap")]
    public async Task RenderPage_RespectsMaximumPixelsCap()
    {
        var renderer = new PdfiumPdfPageRenderer(
            Options.Create(new OcrOptions { MaximumPixels = 1_000_000 }));

        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        var result = await renderer.RenderPageAsync(pdf, 1);

        try
        {
            Assert.True(
                (long)result.Width * result.Height <= 1_000_000);
            Assert.True(result.Width >= 850);
            Assert.True(HasPngMagic(result.ImageStream));
        }
        finally
        {
            result.ImageStream.Dispose();
        }
    }

    [Fact(DisplayName =
        "Rendering an out-of-range page throws the declared PDF category")]
    public async Task OutOfRangePage_ThrowsPdfException()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        await Assert.ThrowsAnyAsync<PdfException>(
            () => _sut.RenderPageAsync(pdf, 99));
    }

    // ------------------------------------------------------------------
    // Stream-safety contract
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The caller's PDF stream is never disposed and never repositioned")]
    public async Task CallerStream_IsNotDisposedAndNotRepositioned()
    {
        using var stream = new ExtractionStubs.DisposeTrackingStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        var result = await _sut.RenderPageAsync(stream, 1);

        try
        {
            Assert.True(HasPngMagic(result.ImageStream));
            Assert.False(stream.IsDisposed);
            Assert.Equal(0, stream.Position);
            Assert.True(stream.CanRead);
        }
        finally
        {
            result.ImageStream.Dispose();
        }
    }

    [Fact(DisplayName =
        "A non-seekable PDF stream is buffered and still rendered")]
    public async Task NonSeekableStream_IsBufferedAndRendered()
    {
        using var inner = new ExtractionStubs.DisposeTrackingStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));
        var nonSeekable = new ExtractionStubs.NonSeekableWrapper(inner);

        Assert.Equal(1, await _sut.GetPageCountAsync(nonSeekable));

        var result = await _sut.RenderPageAsync(nonSeekable, 1);

        try
        {
            Assert.True(HasPngMagic(result.ImageStream));
            Assert.False(inner.IsDisposed);
        }
        finally
        {
            result.ImageStream.Dispose();
        }
    }

    [Fact(DisplayName =
        "A null stream throws an argument error")]
    public async Task NullStream_ThrowsArgumentError()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetPageCountAsync(null!));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.RenderPageAsync(null!, 1));
    }

    [Fact(DisplayName =
        "A zero page number is rejected (pages are one-based)")]
    public async Task ZeroPageNumber_ThrowsArgumentOutOfRange()
    {
        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.RenderPageAsync(pdf, 0));
    }

    [Fact(DisplayName =
        "The caller's PDF stream survives a failed page count (malformed input)")]
    public async Task CallerStream_SurvivesCountFailure()
    {
        using var stream = new ExtractionStubs.DisposeTrackingStream(
            TestPdfBuilder.CreateMalformedPdf());

        await Assert.ThrowsAnyAsync<PdfException>(
            () => _sut.GetPageCountAsync(stream));

        Assert.False(stream.IsDisposed);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact(DisplayName =
        "The caller's PDF stream survives a failed render (malformed input)")]
    public async Task CallerStream_SurvivesRenderFailure()
    {
        using var stream = new ExtractionStubs.DisposeTrackingStream(
            TestPdfBuilder.CreateMalformedPdf());

        await Assert.ThrowsAnyAsync<PdfException>(
            () => _sut.RenderPageAsync(stream, 1));

        Assert.False(stream.IsDisposed);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Cancellation propagates as OperationCanceledException")]
    public async Task CancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var pdf = new MemoryStream(
            TestPdfBuilder.CreateScannedPdf(
                TestImageBuilder.CreateTextJpeg(ScanMarker)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.GetPageCountAsync(pdf, cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.RenderPageAsync(pdf, 1, cts.Token));
    }

    private static bool HasPngMagic(Stream stream)
    {
        var header = new byte[4];
        stream.ReadExactly(header, 0, header.Length);
        stream.Position = 0;

        return header[0] == 0x89
            && header[1] == (byte)'P'
            && header[2] == (byte)'N'
            && header[3] == (byte)'G';
    }
}