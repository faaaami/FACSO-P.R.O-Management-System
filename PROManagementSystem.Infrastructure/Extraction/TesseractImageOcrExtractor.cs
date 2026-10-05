using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;
using TesseractOCR.Exceptions;
using TesseractOCR.InteropDotNet;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Local Tesseract OCR for raster document images. JPEG and PNG files are
/// decoded in memory and recognised for their text layer; scanned PDF pages
/// arrive here as the PNGs produced by <see cref="PdfiumPdfPageRenderer"/>.
/// The caller-owned stream is never disposed nor repositioned. Unrecognisable
/// or unreadable images resolve to an empty result so the extraction pipeline
/// can continue to a pending draft instead of failing the request.
/// </summary>
public sealed class TesseractImageOcrExtractor : IImageOcrExtractor
{
    private readonly OcrOptions _options;

    public TesseractImageOcrExtractor(IOptions<OcrOptions> options)
    {
        _options = options.Value;
    }

    public async Task<OcrTextExtractionResult> ExtractAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (imageStream is null || !imageStream.CanRead)
        {
            return new OcrTextExtractionResult(string.Empty, false);
        }

        var originalPosition = imageStream.CanSeek
            ? imageStream.Position
            : (long?)null;

        byte[] bytes;

        try
        {
            using var buffer = new MemoryStream();

            await imageStream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }
        catch (IOException)
        {
            return new OcrTextExtractionResult(string.Empty, false);
        }
        finally
        {
            if (originalPosition is not null)
            {
                imageStream.Position = originalPosition.Value;
            }
        }

        if (bytes.Length == 0)
        {
            return new OcrTextExtractionResult(string.Empty, false);
        }

        ConfigureNativeSearchPath();

        return await Task.Run(
            () => Recognize(bytes, cancellationToken),
            cancellationToken);
    }

    private OcrTextExtractionResult Recognize(
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var engine = new Engine(
                Path.Combine(AppContext.BaseDirectory, "tessdata"),
                _options.Language,
                EngineMode.LstmOnly);

            using var image = Image.LoadFromMemory(bytes);

            if (_options.MaximumPixels > 0
                && (long)image.Width * image.Height > _options.MaximumPixels)
            {
                return new OcrTextExtractionResult(string.Empty, false);
            }

            using var page = engine.Process(image, PageSegMode.Auto);

            var text = page.Text ?? string.Empty;

            return new OcrTextExtractionResult(
                text,
                HasUsableText: !string.IsNullOrWhiteSpace(text));
        }
        catch (TesseractException)
        {
            return new OcrTextExtractionResult(string.Empty, false);
        }
        catch (IOException)
        {
            return new OcrTextExtractionResult(string.Empty, false);
        }
    }

    /// <summary>
    /// Points the native loader at the directory that contains the Tesseract
    /// and Leptonica binaries for the current platform. Tesseract.Native
    /// places them under <c>runtimes/&lt;rid&gt;/native</c> in the build output;
    /// the TesseractOCR package additionally copies Windows binaries into an
    /// <c>x64</c> folder. The assignment is idempotent so concurrent requests
    /// can share the same static loader configuration.
    /// </summary>
    private static void ConfigureNativeSearchPath()
    {
        var baseDirectory = AppContext.BaseDirectory;

        var runtimeIdentifier =
            RuntimeInformation.RuntimeIdentifier ?? string.Empty;

        var candidateDirectories = new[]
        {
            Path.Combine(
                baseDirectory,
                "runtimes",
                runtimeIdentifier,
                "native"),
            Path.Combine(
                baseDirectory,
                "runtimes",
                runtimeIdentifier.Replace("-x64", string.Empty),
                "native"),
            Path.Combine(baseDirectory, "runtimes", "win-x64", "native"),
            Path.Combine(baseDirectory, "runtimes", "linux-x64", "native"),
            Path.Combine(baseDirectory, "x64"),
            baseDirectory
        };

        foreach (var directory in candidateDirectories)
        {
            if (Directory.Exists(directory))
            {
                LibraryLoader.Instance.CustomSearchPath = directory;
                return;
            }
        }
    }
}