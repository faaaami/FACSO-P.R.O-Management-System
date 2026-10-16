using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Extraction;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Shared fakes for the extraction-path unit tests (Tasks 6B.3-6C). Each
/// stub records how often it was invoked so routing and skip policies can be
/// asserted without any native OCR or PDF engines.
/// </summary>
internal static class ExtractionStubs
{
    internal sealed class StubPdfTextExtractor : IPdfTextExtractor
    {
        public PdfTextExtractionResult Result { get; set; } =
            new(string.Empty, false);

        public Exception? ExceptionToThrow { get; set; }

        public int Calls { get; private set; }

        public Task<PdfTextExtractionResult> ExtractAsync(
            Stream pdfStream,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }

    internal sealed class StubPageRenderer : IPdfPageRenderer
    {
        public int PageCount { get; set; }

        public Exception? CountException { get; set; }

        public Func<int, PdfPageRenderResult> RenderResult { get; set; } =
            _ => new PdfPageRenderResult(new MemoryStream(), 1, 1);

        public Func<int, CancellationToken, Task<PdfPageRenderResult>>?
            AsyncRender { get; set; }

        public Exception? RenderException { get; set; }

        public int CountCalls { get; private set; }

        public int RenderCalls { get; private set; }

        public List<int> RenderedPages { get; } = new();

        public Task<int> GetPageCountAsync(
            Stream pdfStream,
            CancellationToken cancellationToken = default)
        {
            if (CountException is not null)
            {
                throw CountException;
            }

            CountCalls++;

            return Task.FromResult(PageCount);
        }

        public Task<PdfPageRenderResult> RenderPageAsync(
            Stream pdfStream,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            if (RenderException is not null)
            {
                throw RenderException;
            }

            RenderCalls++;
            RenderedPages.Add(pageNumber);

            if (AsyncRender is not null)
            {
                return AsyncRender(pageNumber, cancellationToken);
            }

            return Task.FromResult(RenderResult(pageNumber));
        }
    }

    internal sealed class StubImageOcr : IImageOcrExtractor
    {
        public Func<OcrTextExtractionResult> Result { get; set; } =
            () => new OcrTextExtractionResult(string.Empty, false);

        public Exception? ExceptionToThrow { get; set; }

        public int Calls { get; private set; }

        public Task<OcrTextExtractionResult> ExtractAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result());
        }
    }

    internal sealed class StubDetailsExtractor : IDocumentDetailsExtractor
    {
        public Func<string, CancellationToken, DocumentDetailsExtractionResult>
            ExtractImpl { get; set; } =
                (_, _) => new DocumentDetailsExtractionResult(
                    new Dictionary<string, object?>());

        public Exception? ExceptionToThrow { get; set; }

        public List<(string Text, DocumentType? Type)> Received { get; } = new();

        public Task<DocumentDetailsExtractionResult> ExtractAsync(
            string? text,
            DocumentType? suggestedType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            var effective = text ?? string.Empty;
            Received.Add((effective, suggestedType));

            return Task.FromResult(ExtractImpl(effective, cancellationToken));
        }
    }

    internal sealed class DisposeTrackingStream : MemoryStream
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

    internal sealed class NonSeekableWrapper : Stream
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

    internal sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }
}