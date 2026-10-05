using UglyToad.PdfPig;
using PdfDocumentEncryptedException =
    UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException;
using PdfDocumentFormatException =
    UglyToad.PdfPig.Core.PdfDocumentFormatException;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// PdfPig-backed PDF text extractor. The caller-owned stream is never
/// disposed nor repositioned; non-seekable streams are buffered into memory
/// first. Malformed, encrypted or unreadable PDFs resolve to an empty result
/// so the extraction pipeline can continue to a pending draft instead of
/// failing the whole request.
/// </summary>
public sealed class PdfTextExtractor : IPdfTextExtractor
{
    public async Task<PdfTextExtractionResult> ExtractAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pdfStream is null || !pdfStream.CanRead)
        {
            return new PdfTextExtractionResult(string.Empty, false);
        }

        Stream workingStream = pdfStream;

        if (!pdfStream.CanSeek)
        {
            workingStream = new MemoryStream();

            try
            {
                await pdfStream.CopyToAsync(
                    workingStream,
                    cancellationToken);
            }
            catch (IOException)
            {
                workingStream.Dispose();
                return new PdfTextExtractionResult(string.Empty, false);
            }

            workingStream.Position = 0;
        }
        else if (pdfStream.Length == 0)
        {
            return new PdfTextExtractionResult(string.Empty, false);
        }

        var originalPosition = pdfStream.CanSeek
            ? pdfStream.Position
            : (long?)null;

        try
        {
            return await Task.Run(
                () => ExtractFromPdf(workingStream, cancellationToken),
                cancellationToken);
        }
        finally
        {
            if (originalPosition is not null)
            {
                pdfStream.Position = originalPosition.Value;
            }

            if (!ReferenceEquals(workingStream, pdfStream))
            {
                workingStream.Dispose();
            }
        }
    }

    private static PdfTextExtractionResult ExtractFromPdf(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            using var nonDisposing = new NonDisposingStream(stream);
            using var document = PdfDocument.Open(
                nonDisposing,
                null);

            var pages = new List<string>(document.NumberOfPages);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    pages.Add(page.Text);
                }
            }

            var text = string.Join(Environment.NewLine, pages);

            return new PdfTextExtractionResult(
                text,
                HasUsableText: !string.IsNullOrWhiteSpace(text));
        }
        catch (PdfDocumentFormatException)
        {
            return new PdfTextExtractionResult(string.Empty, false);
        }
        catch (PdfDocumentEncryptedException)
        {
            return new PdfTextExtractionResult(string.Empty, false);
        }
        catch (IOException)
        {
            return new PdfTextExtractionResult(string.Empty, false);
        }
    }

    /// <summary>
    /// Delegates every member to the wrapped stream but never disposes it.
    /// PdfPig takes ownership of the stream handed to it; this guard keeps
    /// the actual upload stream alive for the rest of the pipeline.
    /// </summary>
    private sealed class NonDisposingStream : Stream
    {
        private readonly Stream _inner;

        public NonDisposingStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
            => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken)
            => _inner.FlushAsync(cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => _inner.Seek(offset, origin);

        public override void SetLength(long value)
            => _inner.SetLength(value);

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer)
            => _inner.Read(buffer);

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

        public override void Write(byte[] buffer, int offset, int count)
            => _inner.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer)
            => _inner.Write(buffer);

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => _inner.WriteAsync(buffer, cancellationToken);

        public override void Close()
        {
            // Intentionally no-op: keep the wrapped stream alive.
        }

        protected override void Dispose(bool disposing)
        {
            // Intentionally no-op: keep the wrapped stream alive.
        }

        public override ValueTask DisposeAsync()
            => default;
    }
}