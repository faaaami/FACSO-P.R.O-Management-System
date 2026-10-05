using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.3: unit tests for the PdfPig-backed <see cref="PdfTextExtractor"/>.
/// These cover extraction outcomes across deterministic PDF fixtures plus the
/// stream-safety contract (never disposed, position restored, non-seekable
/// inputs buffered) and the fail-soft policy for unreadable input.
/// </summary>
public class PdfTextExtractorTests
{
    private readonly PdfTextExtractor _sut = new();

    // ------------------------------------------------------------------
    // Extraction outcomes
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A selectable PDF yields its text layer")]
    public async Task SelectablePdf_ExtractsKnownText()
    {
        var pdf = TestPdfBuilder.CreateTextPdf("Hello Fasco PRO");

        var result = await _sut.ExtractAsync(
            new MemoryStream(pdf));

        Assert.True(result.HasUsableText);
        Assert.Contains("Hello Fasco PRO", result.Text);
    }

    [Fact(DisplayName =
        "Multi-page PDF text is combined across pages")]
    public async Task MultiPagePdf_CombinesTexts()
    {
        var pdf = TestPdfBuilder.CreateTextPdf(
            "AlphaPage", "BetaPage", "GammaPage");

        var result = await _sut.ExtractAsync(
            new MemoryStream(pdf));

        Assert.True(result.HasUsableText);
        Assert.Contains("AlphaPage", result.Text);
        Assert.Contains("BetaPage", result.Text);
        Assert.Contains("GammaPage", result.Text);
        Assert.Contains(Environment.NewLine, result.Text);
    }

    [Fact(DisplayName =
        "A scanned-style PDF (no text layer) yields no usable text")]
    public async Task ScannedStylePdf_HasNoUsableText()
    {
        var pdf = TestPdfBuilder.CreateScannedStylePdf();

        var result = await _sut.ExtractAsync(
            new MemoryStream(pdf));

        Assert.False(result.HasUsableText);
    }

    [Fact(DisplayName =
        "A whitespace-only text layer yields no usable text")]
    public async Task WhitespaceOnlyPdf_HasNoUsableText()
    {
        var pdf = TestPdfBuilder.CreateWhitespaceTextPdf();

        var result = await _sut.ExtractAsync(
            new MemoryStream(pdf));

        Assert.False(result.HasUsableText);
    }

    [Fact(DisplayName =
        "A zero-page PDF yields no usable text")]
    public async Task EmptyPageTreePdf_HasNoUsableText()
    {
        var pdf = TestPdfBuilder.CreateEmptyPageTreePdf();

        var result = await _sut.ExtractAsync(
            new MemoryStream(pdf));

        Assert.False(result.HasUsableText);
    }

    // ------------------------------------------------------------------
    // Fail-soft policy
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Malformed PDF bytes never escape the extractor")]
    public async Task MalformedPdf_ReturnsEmptyWithoutThrowing()
    {
        var pdf = TestPdfBuilder.CreateMalformedPdf();

        var result = await _sut.ExtractAsync(
            new MemoryStream(pdf));

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

    // ------------------------------------------------------------------
    // Stream-safety contract
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The caller's stream is never disposed and its position is restored")]
    public async Task CallerStream_IsNotDisposedAndPositionRestored()
    {
        using var stream = new DisposeTrackingStream(
            TestPdfBuilder.CreateTextPdf("Keep Me"));

        var result = await _sut.ExtractAsync(stream);

        Assert.True(result.HasUsableText);
        Assert.False(stream.IsDisposed);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
        Assert.True(stream.Length > 0);
    }

    [Fact(DisplayName =
        "The caller's stream survives malformed input unscathed")]
    public async Task CallerStream_IsNotDisposedOnMalformedInput()
    {
        using var stream = new DisposeTrackingStream(
            TestPdfBuilder.CreateMalformedPdf());

        var result = await _sut.ExtractAsync(stream);

        Assert.False(result.HasUsableText);
        Assert.False(stream.IsDisposed);
        Assert.Equal(0, stream.Position);
    }

    [Fact(DisplayName =
        "A non-seekable stream is buffered, extracted, and left undisposed")]
    public async Task NonSeekableStream_IsBufferedAndExtracted()
    {
        using var inner = new DisposeTrackingStream(
            TestPdfBuilder.CreateTextPdf("Buffered Text"));
        var nonSeekable = new NonSeekableWrapper(inner);

        var result = await _sut.ExtractAsync(nonSeekable);

        Assert.True(result.HasUsableText);
        Assert.Contains("Buffered Text", result.Text);
        Assert.False(inner.IsDisposed);
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

        var pdf = TestPdfBuilder.CreateTextPdf("Never Read");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExtractAsync(new MemoryStream(pdf), cts.Token));
    }

    // ------------------------------------------------------------------
    // Test streams
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