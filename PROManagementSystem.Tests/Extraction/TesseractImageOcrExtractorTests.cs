using Microsoft.Extensions.Options;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.4: unit tests for the Tesseract-backed <see cref="TesseractImageOcrExtractor"/>.
/// Rendered JPEG/PNG fixtures exercise real recognition; the suite also pins
/// the stream-safety contract (never disposed, position restored, non-seekable
/// inputs buffered), the fail-soft policy for unreadable input and the
/// MaximumPixels gate for oversized bitmaps.
/// </summary>
public class TesseractImageOcrExtractorTests
{
    private const string OcrMarker = "OWEN";

    private readonly TesseractImageOcrExtractor _sut = new(
        Options.Create(new OcrOptions()));

    // ------------------------------------------------------------------
    // Recognition outcomes
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A PNG containing text is recognised for its text layer")]
    public async Task TextPng_IsRecognised()
    {
        var png = TestImageBuilder.CreateTextPng(OcrMarker);

        var result = await _sut.ExtractAsync(new MemoryStream(png));

        Assert.True(result.HasUsableText);
        Assert.Contains(OcrMarker, result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "A JPEG containing text is recognised for its text layer")]
    public async Task TextJpeg_IsRecognised()
    {
        var jpeg = TestImageBuilder.CreateTextJpeg(OcrMarker);

        var result = await _sut.ExtractAsync(new MemoryStream(jpeg));

        Assert.True(result.HasUsableText);
        Assert.Contains(OcrMarker, result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "A blank PNG yields no usable text")]
    public async Task BlankPng_HasNoUsableText()
    {
        var result = await _sut.ExtractAsync(
            new MemoryStream(TestImageBuilder.CreateBlankPng()));

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "A blank JPEG yields no usable text")]
    public async Task BlankJpeg_HasNoUsableText()
    {
        var result = await _sut.ExtractAsync(
            new MemoryStream(TestImageBuilder.CreateBlankJpeg()));

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "An image beyond the MaximumPixels gate resolves to an empty result")]
    public async Task OversizedImage_ReturnsEmptyWithoutThrowing()
    {
        var extractor = new TesseractImageOcrExtractor(
            Options.Create(new OcrOptions { MaximumPixels = 1000 }));

        var result = await extractor.ExtractAsync(
            new MemoryStream(TestImageBuilder.CreateTextPng(OcrMarker)));

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    // ------------------------------------------------------------------
    // Fail-soft policy
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Garbage bytes never escape the OCR extractor")]
    public async Task GarbageBytes_ReturnsEmptyWithoutThrowing()
    {
        var result = await _sut.ExtractAsync(
            new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "An empty stream resolves to an empty result without throwing")]
    public async Task EmptyStream_ReturnsEmptyWithoutThrowing()
    {
        var result = await _sut.ExtractAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "A null stream resolves to an empty result without throwing")]
    public async Task NullStream_ReturnsEmptyWithoutThrowing()
    {
        var result = await _sut.ExtractAsync(null!);

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "A stream that fails mid-read resolves to an empty result")]
    public async Task UnreadableStream_ReturnsEmptyWithoutThrowing()
    {
        var result = await _sut.ExtractAsync(new ThrowingReadStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "A truncated PNG (corrupt download) resolves to an empty result")]
    public async Task TruncatedPng_ReturnsEmptyWithoutThrowing()
    {
        var result = await _sut.ExtractAsync(
            new MemoryStream(TestImageBuilder.CreateTruncatedPng()));

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact(DisplayName =
        "A truncated JPEG (corrupt download) resolves to an empty result")]
    public async Task TruncatedJpeg_ReturnsEmptyWithoutThrowing()
    {
        var result = await _sut.ExtractAsync(
            new MemoryStream(TestImageBuilder.CreateTruncatedJpeg()));

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.Text);
    }

    // ------------------------------------------------------------------
    // Stream-safety contract
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The caller's stream is never disposed and its position is restored")]
    public async Task CallerStream_IsNotDisposedAndPositionRestored()
    {
        using var stream = new DisposeTrackingStream(
            TestImageBuilder.CreateTextPng(OcrMarker));

        var result = await _sut.ExtractAsync(stream);

        Assert.True(result.HasUsableText);
        Assert.False(stream.IsDisposed);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
        Assert.True(stream.Length > 0);
    }

    [Fact(DisplayName =
        "A non-seekable stream is buffered, recognised, and left undisposed")]
    public async Task NonSeekableStream_IsBufferedAndRecognised()
    {
        using var inner = new DisposeTrackingStream(
            TestImageBuilder.CreateTextJpeg(OcrMarker));
        var nonSeekable = new NonSeekableWrapper(inner);

        var result = await _sut.ExtractAsync(nonSeekable);

        Assert.True(result.HasUsableText);
        Assert.Contains(OcrMarker, result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.False(inner.IsDisposed);
    }

    [Fact(DisplayName =
        "The caller's stream is never disposed and its position restored on corrupt input")]
    public async Task CallerStream_IsNotDisposedOnCorruptInput()
    {
        using var stream = new DisposeTrackingStream(
            TestImageBuilder.CreateTruncatedPng());

        var result = await _sut.ExtractAsync(stream);

        Assert.False(result.HasUsableText);
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExtractAsync(
                new MemoryStream(TestImageBuilder.CreateTextPng(OcrMarker)),
                cts.Token));
    }

    // ------------------------------------------------------------------
    // Test streams (mirrors PdfTextExtractorTests)
    // ------------------------------------------------------------------

    private sealed class DisposeTrackingStream : MemoryStream
    {
        public DisposeTrackingStream(byte[] content)
            : base(content, writable: true)
        {
        }

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class NonSeekableWrapper : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableWrapper(MemoryStream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => 4096;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new IOException("simulated read failure");

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}