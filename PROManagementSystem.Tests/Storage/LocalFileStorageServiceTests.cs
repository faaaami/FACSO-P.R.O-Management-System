using Microsoft.Extensions.Options;
using PROManagementSystem.Infrastructure.Storage;
using Xunit;

namespace PROManagementSystem.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="LocalFileStorageService"/>. Each test runs
/// against its own unique temporary storage root, which is deleted in a
/// finally block so cleanup happens even when the test fails.
/// </summary>
public class LocalFileStorageServiceTests
{
    private static readonly byte[] PdfBytes =
        [0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37]; // "%PDF-1.7"

    private static readonly byte[] JpegBytes =
        [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10]; // JPEG SOI

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]; // PNG signature

    // ------------------------------------------------------------------
    // Fixture
    // ------------------------------------------------------------------

    private sealed class TempRoot : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"proms-unit-{Guid.NewGuid():N}");

        public LocalFileStorageService CreateService()
            => new(
                Options.Create(
                    new StorageOptions { RootPath = Path }));

        public string Resolve(string reference)
            => System.IO.Path.Combine(
                Path,
                reference.Replace(
                    '/',
                    System.IO.Path.DirectorySeparatorChar));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static async Task<(string Reference, string FullPath)> SaveAsync(
        LocalFileStorageService service,
        TempRoot root,
        byte[] content,
        string fileName,
        string contentType)
    {
        await using var stream =
            new MemoryStream(content);

        var reference = await service.SaveAsync(
            stream,
            fileName,
            contentType,
            CancellationToken.None);

        return (reference, root.Resolve(reference));
    }

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A: saves a PDF successfully")]
    public async Task A_SavesPdfSuccessfully()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service, root, PdfBytes, "passport.pdf", "application/pdf");

        Assert.True(File.Exists(fullPath));
        Assert.EndsWith(".pdf", reference, StringComparison.Ordinal);
        Assert.Equal(PdfBytes, await File.ReadAllBytesAsync(fullPath));
    }

    [Fact(DisplayName =
        "B: saves a JPEG successfully")]
    public async Task B_SavesJpegSuccessfully()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service, root, JpegBytes, "photo.jpeg", "image/jpeg");

        Assert.True(File.Exists(fullPath));
        Assert.EndsWith(".jpg", reference, StringComparison.Ordinal);
        Assert.Equal(JpegBytes, await File.ReadAllBytesAsync(fullPath));
    }

    [Fact(DisplayName =
        "C: saves a PNG successfully")]
    public async Task C_SavesPngSuccessfully()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service, root, PngBytes, "logo.png", "image/png");

        Assert.True(File.Exists(fullPath));
        Assert.EndsWith(".png", reference, StringComparison.Ordinal);
        Assert.Equal(PngBytes, await File.ReadAllBytesAsync(fullPath));
    }

    [Fact(DisplayName =
        "D: returned reference is relative, not absolute")]
    public async Task D_ReferenceIsRelative()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, _) = await SaveAsync(
            service, root, PdfBytes, "a.pdf", "application/pdf");

        Assert.False(Path.IsPathRooted(reference));
        Assert.DoesNotContain(":", reference, StringComparison.Ordinal);
    }

    [Fact(DisplayName =
        "E: generated reference contains a unique server-generated component")]
    public async Task E_ReferenceContainsUniqueComponent()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, _) = await SaveAsync(
            service, root, PdfBytes, "a.pdf", "application/pdf");

        Assert.StartsWith(
            "extractions/",
            reference,
            StringComparison.Ordinal);

        var key = reference[
            "extractions/".Length..];
        var dotIndex = key.LastIndexOf('.');
        var guidPart = dotIndex >= 0 ? key[..dotIndex] : key;

        Assert.True(
            Guid.TryParseExact(guidPart, "N", out _),
            $"Reference '{reference}' did not contain a GUID key.");

        // Two saves never reuse the same key.
        var (secondReference, _) = await SaveAsync(
            service, root, PdfBytes, "b.pdf", "application/pdf");
        Assert.NotEqual(reference, secondReference);
    }

    [Fact(DisplayName =
        "F: original filename is not used as an unsafe path")]
    public async Task F_OriginalFilenameNotUsedAsPath()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service,
            root,
            PdfBytes,
            "../../escape-me.pdf",
            "application/pdf");

        Assert.DoesNotContain("escape", reference, StringComparison.Ordinal);
        Assert.StartsWith(
            root.Path + System.IO.Path.DirectorySeparatorChar,
            fullPath,
            StringComparison.Ordinal);
        Assert.True(File.Exists(fullPath));
    }

    [Fact(DisplayName =
        "G: traversal filename cannot escape storage root")]
    public async Task G_TraversalFilenameCannotEscape()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var escapedLocation =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "proms-escape-",
                "evil.pdf");

        var (reference, fullPath) = await SaveAsync(
            service,
            root,
            PdfBytes,
            @"..\..\evil.pdf",
            "application/pdf");

        Assert.True(File.Exists(fullPath));
        Assert.False(File.Exists(escapedLocation));

        // The reference itself must not contain traversal segments.
        Assert.DoesNotContain("..", reference, StringComparison.Ordinal);
    }

    [Fact(DisplayName =
        "H: absolute path filename cannot escape storage root")]
    public async Task H_AbsoluteFilenameCannotEscape()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (_, fullPath) = await SaveAsync(
            service,
            root,
            PdfBytes,
            @"C:\tmp\evil.pdf",
            "application/pdf");

        Assert.True(File.Exists(fullPath));
        Assert.StartsWith(
            root.Path + System.IO.Path.DirectorySeparatorChar,
            fullPath,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName =
        "I: UNC/path traversal attempts cannot escape storage root")]
    public async Task I_UncFilenameCannotEscape()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (_, fullPath) = await SaveAsync(
            service,
            root,
            PdfBytes,
            @"\\server\share\evil.pdf",
            "application/pdf");

        Assert.True(File.Exists(fullPath));
        Assert.StartsWith(
            root.Path + System.IO.Path.DirectorySeparatorChar,
            fullPath,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName =
        "J: saved file contents match the input stream")]
    public async Task J_SavedContentsMatchInput()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var expected = Guid.NewGuid().ToByteArray();
        var (_, fullPath) = await SaveAsync(
            service, root, expected, "data.pdf", "application/pdf");

        var actual = await File.ReadAllBytesAsync(fullPath);
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName =
        "K: cancellation is respected before writing")]
    public async Task K_CancellationIsRespected()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        using var stream = new MemoryStream(PdfBytes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var saved = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                await service.SaveAsync(
                    stream,
                    "a.pdf",
                    "application/pdf",
                    cts.Token);

                saved = true;
            });

        Assert.False(saved);
        Assert.False(Directory.Exists(root.Path));
    }

    [Fact(DisplayName =
        "L: DeleteAsync removes the saved file")]
    public async Task L_DeleteAsyncRemovesFile()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service, root, PdfBytes, "a.pdf", "application/pdf");

        Assert.True(File.Exists(fullPath));

        await service.DeleteAsync(
            reference,
            CancellationToken.None);

        Assert.False(File.Exists(fullPath));

        // Deleting a missing reference is a no-op.
        await service.DeleteAsync(
            reference,
            CancellationToken.None);
    }

    [Fact(DisplayName =
        "M: SaveThenRead_ReturnsExactBytes")]
    public async Task M_SaveThenRead_ReturnsExactBytes()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, _) = await SaveAsync(
            service, root, PdfBytes, "passport.pdf", "application/pdf");

        var stream = await service.ReadAsync(
            reference, CancellationToken.None);

        try
        {
            Assert.NotNull(stream);
            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);
            Assert.Equal(PdfBytes, memory.ToArray());
        }
        finally
        {
            stream?.Dispose();
        }
    }

    [Fact(DisplayName =
        "N: ReadRestoresExpectedStreamBehavior")]
    public async Task N_ReadRestoresExpectedStreamBehavior()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, _) = await SaveAsync(
            service, root, PdfBytes, "passport.pdf", "application/pdf");

        var stream = await service.ReadAsync(
            reference, CancellationToken.None);

        try
        {
            Assert.NotNull(stream);
            Assert.True(stream!.CanRead);
            Assert.True(stream.CanSeek);
            Assert.Equal(0, stream.Position);
            Assert.Equal(PdfBytes.Length, stream.Length);

            // InputStream position starts at zero regardless of prior reads.
            var buffer = new byte[4];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await stream.ReadAsync(
                    buffer.AsMemory(read, buffer.Length - read));
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            Assert.Equal(buffer.Length, read);
            Assert.Equal(4, stream.Position);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    [Fact(DisplayName =
        "O: MissingFile_ReturnsNotFound")]
    public async Task O_MissingFile_ReturnsNotFound()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service, root, PdfBytes, "a.pdf", "application/pdf");

        File.Delete(fullPath);

        var stream = await service.ReadAsync(
            reference, CancellationToken.None);

        Assert.Null(stream);
    }

    [Fact(DisplayName =
        "P: TraversalReference_IsRejected")]
    public async Task P_TraversalReference_IsRejected()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var stream = await service.ReadAsync(
            "../../escape.pdf", CancellationToken.None);

        Assert.Null(stream);
    }

    [Fact(DisplayName =
        "Q: RootedReference_IsRejected")]
    public async Task Q_RootedReference_IsRejected()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var stream = await service.ReadAsync(
            @"C:\tmp\evil.pdf", CancellationToken.None);

        Assert.Null(stream);
    }

    [Fact(DisplayName =
        "R: UNCReference_IsRejected")]
    public async Task R_UncReference_IsRejected()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var stream = await service.ReadAsync(
            @"\\server\share\evil.pdf", CancellationToken.None);

        Assert.Null(stream);
    }

    [Fact(DisplayName =
        "S: ReadDoesNotDeleteFile")]
    public async Task S_ReadDoesNotDeleteFile()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var (reference, fullPath) = await SaveAsync(
            service, root, PdfBytes, "a.pdf", "application/pdf");

        Assert.True(File.Exists(fullPath));

        var stream = await service.ReadAsync(
            reference, CancellationToken.None);

        stream?.Dispose();

        Assert.True(File.Exists(fullPath));

        // The file is still readable afterwards.
        var secondStream = await service.ReadAsync(
            reference, CancellationToken.None);

        try
        {
            Assert.NotNull(secondStream);
        }
        finally
        {
            secondStream?.Dispose();
        }
    }

    [Fact(DisplayName =
        "T: LargeFile_IsStreamedThroughFileStream")]
    public async Task T_LargeFile_IsStreamedThroughFileStream()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        var largeBytes = new byte[5 * 1024 * 1024];
        new Random(42).NextBytes(largeBytes);

        var (reference, _) = await SaveAsync(
            service, root, largeBytes, "large.pdf", "application/pdf");

        var stream = await service.ReadAsync(
            reference, CancellationToken.None);

        try
        {
            // The read path opens a FileStream, not a fully-buffered
            // MemoryStream, so large files are streamed to the consumer.
            Assert.NotNull(stream);
            Assert.IsAssignableFrom<FileStream>(stream);

            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);

            Assert.Equal(largeBytes, memory.ToArray());
        }
        finally
        {
            stream?.Dispose();
        }
    }

    [Fact(DisplayName =
        "U: EmptyReference_ReturnsNotFound")]
    public async Task U_EmptyReference_ReturnsNotFound()
    {
        using var root = new TempRoot();
        var service = root.CreateService();

        Assert.Null(await service.ReadAsync(
            null!, CancellationToken.None));
        Assert.Null(await service.ReadAsync(
            string.Empty, CancellationToken.None));
        Assert.Null(await service.ReadAsync(
            "   ", CancellationToken.None));
    }
}