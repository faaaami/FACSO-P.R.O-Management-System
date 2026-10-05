using System.Linq;
using PROManagementSystem.Application.Common.FileValidation;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Pure unit tests for <see cref="FileSignatureValidator"/>. No database or
/// web-host required; operates entirely on in-memory streams.
/// </summary>
public class FileSignatureValidatorTests
{
    private static readonly byte[] PdfSignature =
        [0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34];

    private static readonly byte[] JpegSignature =
        [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46];

    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    // ------------------------------------------------------------------
    // Acceptance
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Valid PDF signature is accepted")]
    public void ValidPdf_IsAccepted()
        => Assert.True(
            FileSignatureValidator.IsSignatureValid(
                "application/pdf",
                new MemoryStream(PdfSignature)));

    [Fact(DisplayName = "Valid JPEG signature is accepted")]
    public void ValidJpeg_IsAccepted()
        => Assert.True(
            FileSignatureValidator.IsSignatureValid(
                "image/jpeg",
                new MemoryStream(JpegSignature)));

    [Fact(DisplayName = "Valid PNG signature is accepted")]
    public void ValidPng_IsAccepted()
        => Assert.True(
            FileSignatureValidator.IsSignatureValid(
                "image/png",
                new MemoryStream(PngSignature)));

    // ------------------------------------------------------------------
    // Rejection: wrong bytes
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Zeros with PDF content type are rejected")]
    public void FakePdf_IsRejected()
    {
        using var stream = new MemoryStream(new byte[16]);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "application/pdf",
                stream));
    }

    [Fact(DisplayName = "Zeros with JPEG content type are rejected")]
    public void FakeJpeg_IsRejected()
    {
        using var stream = new MemoryStream(new byte[16]);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "image/jpeg",
                stream));
    }

    [Fact(DisplayName = "Zeros with PNG content type are rejected")]
    public void FakePng_IsRejected()
    {
        using var stream = new MemoryStream(new byte[16]);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "image/png",
                stream));
    }

    // ------------------------------------------------------------------
    // MIME / signature mismatch
    // ------------------------------------------------------------------

    [Fact(DisplayName = "PDF bytes with image/jpeg content type are rejected")]
    public void Mismatch_PdfBytes_JpegContentType_IsRejected()
    {
        using var stream = new MemoryStream(PdfSignature);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "image/jpeg",
                stream));
    }

    [Fact(DisplayName = "JPEG bytes with application/pdf content type are rejected")]
    public void Mismatch_JpegBytes_PdfContentType_IsRejected()
    {
        using var stream = new MemoryStream(JpegSignature);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "application/pdf",
                stream));
    }

    [Fact(DisplayName = "PNG bytes with image/jpeg content type are rejected")]
    public void Mismatch_PngBytes_JpegContentType_IsRejected()
    {
        using var stream = new MemoryStream(PngSignature);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "image/jpeg",
                stream));
    }

    [Fact(DisplayName = "PDF bytes with image/png content type are rejected")]
    public void Mismatch_PdfBytes_PngContentType_IsRejected()
    {
        using var stream = new MemoryStream(PdfSignature);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "image/png",
                stream));
    }

    // ------------------------------------------------------------------
    // Edge cases
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Empty stream is rejected")]
    public void EmptyStream_IsRejected()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "application/pdf",
                stream));
    }

    [Fact(DisplayName = "Too-short stream (fewer bytes than signature) is rejected")]
    public void TooShortStream_IsRejected()
    {
        using var stream = new MemoryStream([0x25, 0x50]); // only "%P"
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "application/pdf",
                stream));
    }

    [Fact(DisplayName = "Null content type returns false")]
    public void NullContentType_IsRejected()
    {
        using var stream = new MemoryStream(PdfSignature);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                null,
                stream));
    }

    [Fact(DisplayName = "Null stream returns false")]
    public void NullStream_IsRejected()
        => Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "application/pdf",
                null!));

    [Fact(DisplayName = "Unrecognized content type returns false")]
    public void UnrecognizedContentType_IsRejected()
    {
        using var stream = new MemoryStream(PdfSignature);
        Assert.False(
            FileSignatureValidator.IsSignatureValid(
                "text/plain",
                stream));
    }

    // ------------------------------------------------------------------
    // Stream position restoration
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Stream position is restored to 0 after a passing validation")]
    public void StreamPosition_Restored_AfterPassingValidation()
    {
        var bytes = new byte[] {
            0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, 0x10, 0x20
        };
        using var stream = new MemoryStream(bytes);

        var result = FileSignatureValidator.IsSignatureValid(
            "application/pdf",
            stream);

        Assert.True(result);
        Assert.Equal(0, stream.Position);
    }

    [Fact(DisplayName = "Stream position is restored to 0 after a failing validation")]
    public void StreamPosition_Restored_AfterFailingValidation()
    {
        var bytes = new byte[16];
        using var stream = new MemoryStream(bytes);

        var result = FileSignatureValidator.IsSignatureValid(
            "application/pdf",
            stream);

        Assert.False(result);
        Assert.Equal(0, stream.Position);
    }

    [Fact(DisplayName = "Stream position is restored to its original non-zero offset")]
    public void StreamPosition_Restored_ToOriginalOffset()
    {
        var bytes = new byte[] {
            0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x34, 0x10, 0x20
        };
        using var stream = new MemoryStream(bytes);

        // Seek past the first 3 bytes to simulate a partially consumed stream.
        stream.Position = 3;

        FileSignatureValidator.IsSignatureValid(
            "application/pdf",
            stream);

        Assert.Equal(3, stream.Position);
    }

    [Fact(DisplayName = "Consecutive calls leave the stream fully intact")]
    public void ConsecutiveCalls_PreservesStream()
    {
        var bytes = new byte[] {
            0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x10, 0x20
        };
        using var stream = new MemoryStream(bytes);

        var first = FileSignatureValidator.IsSignatureValid(
            "application/pdf",
            stream);

        var second = FileSignatureValidator.IsSignatureValid(
            "application/pdf",
            stream);

        Assert.True(first);
        Assert.True(second);

        stream.Position = 0;
        Assert.Equal(bytes, stream.ToArray());
    }

    // ------------------------------------------------------------------
    // Non-seekable stream handling
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Non-seekable stream is skipped gracefully without consuming bytes")]
    public void NonSeekableStream_SkippedWithoutConsuming()
    {
        var payload = new byte[] {
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
            0xaa, 0xbb, 0xcc
        };
        using var inner = new MemoryStream(payload);

        var wrapper = new NonSeekableStreamWrapper(inner);

        // Should return true (skip) rather than false or throw.
        var result = FileSignatureValidator.IsSignatureValid(
            "image/png",
            wrapper);

        Assert.True(result);

        // The inner stream is still at position 0 and its bytes are
        // untouched, proving the wrapper wasn't consumed.
        inner.Position = 0;
        Assert.Equal(payload[0], inner.ReadByte());
        Assert.Equal(payload[1], inner.ReadByte());
    }

    // ------------------------------------------------------------------
    // Nested helper
    // ------------------------------------------------------------------

    /// <summary>
    /// A minimal stream wrapper that reports CanSeek = false while delegating
    /// everything else to the inner stream.
    /// </summary>
    private sealed class NonSeekableStreamWrapper(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] b, int off, int c)
            => inner.Read(b, off, c);
        public override long Seek(long o, SeekOrigin w)
            => throw new NotSupportedException();
        public override void SetLength(long v)
            => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c)
            => throw new NotSupportedException();
    }
}