using ImageMagick;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.FileValidation;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Extraction;

public enum ImagePreparationProblem
{
    None,

    /// <summary>The bytes could not be decoded as an image.</summary>
    Unreadable,

    /// <summary>Decoded, but too small for OCR to read reliably.</summary>
    TooSmall
}

/// <summary>
/// An image normalised for OCR: EXIF orientation applied, grayscale,
/// contrast-stretched, PNG-encoded, long edge at most
/// <see cref="OcrOptions.MaxImageLongEdge"/>.
/// </summary>
public sealed record PreparedImage(
    byte[] Png,
    int Width,
    int Height,
    ImagePreparationProblem Problem)
{
    public static PreparedImage Failed(ImagePreparationProblem problem)
        => new(Array.Empty<byte>(), 0, 0, problem);

    public bool IsUsable => Problem == ImagePreparationProblem.None;
}

public interface IImagePreprocessor
{
    /// <summary>Prepares a JPEG, PNG or HEIC image for OCR.</summary>
    Task<PreparedImage> PrepareAsync(Stream image, CancellationToken cancellationToken);

    /// <summary>Rotates a prepared image by 90, 180 or 270 degrees.</summary>
    byte[] Rotate(byte[] png, int degrees);

    /// <summary>
    /// Returns the bottom part of a prepared image (where an MRZ sits),
    /// <paramref name="fraction"/> of its height.
    /// </summary>
    byte[] CropBottom(byte[] png, double fraction);
}

/// <summary>
/// Magick.NET implementation of OCR preparation and HEIC-to-JPEG storage
/// conversion. Images are never logged or written anywhere except the
/// returned buffers.
/// </summary>
public sealed class ImagePreprocessor : IImagePreprocessor, IUploadImageConverter
{
    private const int JpegQuality = 90;

    private readonly OcrOptions _options;

    static ImagePreprocessor()
    {
        // Decompression-bomb guard for every Magick.NET decode in the process.
        ResourceLimits.Width = 20_000;
        ResourceLimits.Height = 20_000;
        ResourceLimits.Area = 200_000_000;
    }

    public ImagePreprocessor(IOptions<OcrOptions> options)
    {
        _options = options.Value;
    }

    public async Task<PreparedImage> PrepareAsync(
        Stream image,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadAllAsync(image, cancellationToken);

        if (bytes.Length == 0)
        {
            return PreparedImage.Failed(ImagePreparationProblem.Unreadable);
        }

        return await Task.Run(() => Prepare(bytes), cancellationToken);
    }

    public byte[] Rotate(byte[] png, int degrees)
    {
        using var image = new MagickImage(png);
        image.Rotate(degrees);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    public byte[] CropBottom(byte[] png, double fraction)
    {
        using var image = new MagickImage(png);
        var height = (uint)Math.Max(1, Math.Round(image.Height * Math.Clamp(fraction, 0.05, 1)));
        image.Crop(new MagickGeometry(0, (int)(image.Height - height), image.Width, height));
        image.ResetPage();
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    public async Task<StoredUpload?> ConvertForStorageAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!FileUploadPolicy.IsHeic(contentType))
        {
            return null;
        }

        var bytes = await ReadAllAsync(content, cancellationToken);

        byte[] jpeg;

        try
        {
            jpeg = await Task.Run(
                () =>
                {
                    using var image = new MagickImage(bytes);
                    image.AutoOrient();
                    image.Format = MagickFormat.Jpeg;
                    image.Quality = JpegQuality;
                    return image.ToByteArray();
                },
                cancellationToken);
        }
        catch (MagickException exception)
        {
            throw new InvalidOperationException(
                "The HEIC photo could not be read. Please upload a JPG, PNG or PDF instead.",
                exception);
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);

        return new StoredUpload(
            new MemoryStream(jpeg, writable: false),
            (string.IsNullOrWhiteSpace(baseName) ? "photo" : baseName) + ".jpg",
            "image/jpeg",
            jpeg.Length);
    }

    private PreparedImage Prepare(byte[] bytes)
    {
        try
        {
            using var image = new MagickImage(bytes);

            image.AutoOrient();
            image.Grayscale();
            // Contrast stretch: maps the darkest 2% to black and the
            // brightest 1% to white, which lifts faded scans and dim photos.
            image.ContrastStretch(new Percentage(2), new Percentage(1));

            var longEdge = Math.Max(image.Width, image.Height);

            if (_options.MaxImageLongEdge > 0 && longEdge > _options.MaxImageLongEdge)
            {
                image.Resize(new MagickGeometry(
                    (uint)_options.MaxImageLongEdge,
                    (uint)_options.MaxImageLongEdge));
            }

            image.Format = MagickFormat.Png;
            var png = image.ToByteArray();

            var shortEdge = (int)Math.Min(image.Width, image.Height);
            var problem = shortEdge < _options.MinImageShortEdge
                ? ImagePreparationProblem.TooSmall
                : ImagePreparationProblem.None;

            return new PreparedImage(png, (int)image.Width, (int)image.Height, problem);
        }
        catch (MagickException)
        {
            return PreparedImage.Failed(ImagePreparationProblem.Unreadable);
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return buffer.ToArray();
    }
}
