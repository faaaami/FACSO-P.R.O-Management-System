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

    public Task<OcrTextExtractionResult> ExtractAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
        => ExtractAsync(imageStream, new OcrRecognitionOptions(), cancellationToken);

    public async Task<OcrTextExtractionResult> ExtractAsync(
        Stream imageStream,
        OcrRecognitionOptions options,
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
            () => Recognize(bytes, options, cancellationToken),
            cancellationToken);
    }

    private OcrTextExtractionResult Recognize(
        byte[] bytes,
        OcrRecognitionOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var engine = new Engine(
                Path.Combine(AppContext.BaseDirectory, "tessdata"),
                _options.Language,
                EngineMode.LstmOnly);

            // Never hand raw upload bytes to Leptonica: on Linux a corrupt or
            // truncated PNG/JPEG can abort the whole process inside libpng /
            // libjpeg. Magick.NET decodes defensively (corruption surfaces as
            // a managed exception) and re-encodes a clean PNG for Leptonica.
            var safeBytes = NormalizeForLeptonica(bytes, _options.MaximumPixels);

            if (safeBytes is null)
            {
                return new OcrTextExtractionResult(string.Empty, false);
            }

            using var image = Image.LoadFromMemory(safeBytes);

            if (_options.MaximumPixels > 0
                && (long)image.Width * image.Height > _options.MaximumPixels)
            {
                return new OcrTextExtractionResult(string.Empty, false);
            }

            if (!string.IsNullOrEmpty(options.CharacterWhitelist))
            {
                engine.SetVariable("tessedit_char_whitelist", options.CharacterWhitelist);
            }

            using var page = engine.Process(
                image,
                options.SingleBlock ? PageSegMode.SingleBlock : PageSegMode.Auto);

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
    /// <summary>
    /// Decodes the upload with Magick.NET and returns a lossless PNG of it, or
    /// null when the image is unreadable or larger than the pixel gate. The
    /// header is inspected first so oversized images are rejected without
    /// being decoded.
    /// </summary>
    internal static byte[]? NormalizeForLeptonica(byte[] bytes, long maximumPixels)
    {
        try
        {
            var info = new ImageMagick.MagickImageInfo(bytes);

            if (maximumPixels > 0 && (long)info.Width * info.Height > maximumPixels)
            {
                return null;
            }

            using var magick = new ImageMagick.MagickImage(bytes);
            magick.AutoOrient();
            magick.Format = ImageMagick.MagickFormat.Png;
            return magick.ToByteArray();
        }
        catch (ImageMagick.MagickException)
        {
            return null;
        }
    }

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