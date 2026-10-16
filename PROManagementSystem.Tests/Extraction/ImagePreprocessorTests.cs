using ImageMagick;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.FileValidation;
using PROManagementSystem.Infrastructure.Extraction;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// OCR image preparation (EXIF orientation, grayscale, contrast, resize) and
/// HEIC handling. Fixtures are generated in memory; no real documents.
/// </summary>
public sealed class ImagePreprocessorTests
{
    private static ImagePreprocessor NewPreprocessor(int maxLongEdge = 2400, int minShortEdge = 600)
        => new(Options.Create(new OcrOptions
        {
            MaxImageLongEdge = maxLongEdge,
            MinImageShortEdge = minShortEdge
        }));

    private static MemoryStream Jpeg(uint width, uint height, ushort exifOrientation = 1)
    {
        using var image = new MagickImage(MagickColors.SkyBlue, width, height);
        image.Format = MagickFormat.Jpeg;

        // A real EXIF Orientation tag, as a phone camera writes it. ImageMagick
        // writes the tag from the image's orientation attribute, so both are set.
        image.Orientation = (OrientationType)exifOrientation;
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, exifOrientation);
        image.SetProfile(exif);

        var bytes = image.ToByteArray();

        // Guard the fixture itself: the written JPEG must carry the tag.
        using var check = new MagickImage(bytes);
        Assert.Equal((OrientationType)exifOrientation, check.Orientation);

        return new MemoryStream(bytes);
    }

    [Fact(DisplayName = "EXIF orientation is applied (a sideways phone photo comes out upright)")]
    public async Task ExifOrientation_IsApplied()
    {
        // Stored 1600×800 with "rotate 90° clockwise" in EXIF → upright 800×1600.
        await using var photo = Jpeg(1600, 800, exifOrientation: 6);

        var prepared = await NewPreprocessor().PrepareAsync(photo, CancellationToken.None);

        Assert.True(prepared.IsUsable);
        Assert.Equal(800, prepared.Width);
        Assert.Equal(1600, prepared.Height);
    }

    [Fact(DisplayName = "Output is a grayscale PNG")]
    public async Task Output_IsGrayscalePng()
    {
        await using var photo = Jpeg(1200, 800);

        var prepared = await NewPreprocessor().PrepareAsync(photo, CancellationToken.None);

        using var result = new MagickImage(prepared.Png);
        Assert.Equal(MagickFormat.Png, result.Format);
        Assert.True(
            result.ColorSpace == ColorSpace.Gray || result.ColorType is ColorType.Grayscale or ColorType.GrayscaleAlpha,
            $"Expected grayscale, got {result.ColorSpace}/{result.ColorType}");
    }

    [Fact(DisplayName = "Large photos are downscaled, not skipped")]
    public async Task LargePhoto_IsDownscaled()
    {
        await using var photo = Jpeg(6000, 3000);

        var prepared = await NewPreprocessor().PrepareAsync(photo, CancellationToken.None);

        Assert.True(prepared.IsUsable);
        Assert.Equal(2400, prepared.Width);
        Assert.Equal(1200, prepared.Height);
    }

    [Fact(DisplayName = "Tiny images are reported as too small")]
    public async Task TinyImage_IsTooSmall()
    {
        await using var photo = Jpeg(300, 200);

        var prepared = await NewPreprocessor().PrepareAsync(photo, CancellationToken.None);

        Assert.Equal(ImagePreparationProblem.TooSmall, prepared.Problem);
    }

    [Fact(DisplayName = "Non-image bytes are reported as unreadable")]
    public async Task Garbage_IsUnreadable()
    {
        await using var garbage = new MemoryStream("not an image at all"u8.ToArray());

        var prepared = await NewPreprocessor().PrepareAsync(garbage, CancellationToken.None);

        Assert.Equal(ImagePreparationProblem.Unreadable, prepared.Problem);
    }

    [Fact(DisplayName = "Rotate and bottom crop work on prepared images")]
    public async Task RotateAndCrop()
    {
        await using var photo = Jpeg(1200, 800);
        var preprocessor = NewPreprocessor();
        var prepared = await preprocessor.PrepareAsync(photo, CancellationToken.None);

        using var rotated = new MagickImage(preprocessor.Rotate(prepared.Png, 90));
        Assert.Equal(800u, rotated.Width);
        Assert.Equal(1200u, rotated.Height);

        using var cropped = new MagickImage(preprocessor.CropBottom(prepared.Png, 0.25));
        Assert.Equal(1200u, cropped.Width);
        Assert.Equal(200u, cropped.Height);
    }

    [Fact(DisplayName = "Non-HEIC uploads are stored unchanged")]
    public async Task NonHeic_IsNotConverted()
    {
        await using var photo = Jpeg(1200, 800);

        var converted = await NewPreprocessor().ConvertForStorageAsync(
            photo, "scan.jpg", "image/jpeg", CancellationToken.None);

        Assert.Null(converted);
    }

    [Fact(DisplayName = "HEIC photos are converted to an upright JPEG for storage")]
    public async Task Heic_IsConvertedToJpeg()
    {
        await using var heic = new MemoryStream(HeicFixture.Bytes());

        await using var converted = await NewPreprocessor().ConvertForStorageAsync(
            heic, "IMG_0001.HEIC", "image/heic", CancellationToken.None);

        Assert.NotNull(converted);
        Assert.Equal("IMG_0001.jpg", converted!.FileName);
        Assert.Equal("image/jpeg", converted.ContentType);

        using var jpeg = new MagickImage(converted.Content);
        Assert.Equal(MagickFormat.Jpeg, jpeg.Format);
        Assert.Equal(converted.FileSize, converted.Content.Length);
    }

    [Fact(DisplayName = "HEIC photos are prepared for OCR")]
    public async Task Heic_IsPreparedForOcr()
    {
        await using var heic = new MemoryStream(HeicFixture.Bytes());

        var prepared = await NewPreprocessor().PrepareAsync(heic, CancellationToken.None);

        Assert.True(prepared.IsUsable);
        Assert.Equal(1600, prepared.Width);
    }

    [Fact(DisplayName = "A corrupt HEIC upload gives a clear error")]
    public async Task CorruptHeic_Throws()
    {
        var bytes = HeicFixture.Bytes()[..64];
        await using var corrupt = new MemoryStream(bytes);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewPreprocessor().ConvertForStorageAsync(
                corrupt, "x.heic", "image/heic", CancellationToken.None));

        Assert.Contains("HEIC", error.Message);
    }
}

public sealed class HeicUploadPolicyTests
{
    [Theory(DisplayName = "HEIC/HEIF uploads are accepted with matching extensions")]
    [InlineData("image/heic", "IMG_0001.HEIC")]
    [InlineData("image/heif", "photo.heif")]
    [InlineData("image/heic", "photo.heif")]
    public void HeicPairs_AreAllowed(string contentType, string fileName)
    {
        Assert.True(FileUploadPolicy.IsContentTypeAllowed(contentType));
        Assert.True(FileUploadPolicy.IsMimeExtensionPairValid(contentType, fileName));
    }

    [Theory(DisplayName = "Generic content types are inferred from a .heic/.heif name only")]
    [InlineData("application/octet-stream", "IMG_1.heic", "image/heic")]
    [InlineData("", "IMG_1.HEIF", "image/heif")]
    [InlineData(null, "IMG_1.heic", "image/heic")]
    [InlineData("application/octet-stream", "file.pdf", "application/octet-stream")]
    [InlineData("image/jpeg", "IMG_1.heic", "image/jpeg")]
    public void NormalizeContentType(string? contentType, string fileName, string expected)
        => Assert.Equal(expected, FileUploadPolicy.NormalizeContentType(contentType, fileName));

    [Fact(DisplayName = "A real HEIC file passes the signature check")]
    public void HeicSignature_IsValid()
    {
        using var heic = new MemoryStream(HeicFixture.Bytes());

        Assert.True(FileSignatureValidator.IsSignatureValid("image/heic", heic));
        Assert.Equal(0, heic.Position);
    }

    [Fact(DisplayName = "An MP4 file declared as HEIC fails the signature check")]
    public void Mp4AsHeic_IsRejected()
    {
        // ISO media header with the "isom" brand (MP4), not a HEIF brand.
        var mp4 = new byte[] { 0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
                               (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0 };

        using var stream = new MemoryStream(mp4);
        Assert.False(FileSignatureValidator.IsSignatureValid("image/heic", stream));
    }

    [Fact(DisplayName = "A JPEG declared as HEIC fails the signature check")]
    public void JpegAsHeic_IsRejected()
    {
        using var stream = new MemoryStream(new byte[] { 0xff, 0xd8, 0xff, 0xe0, 0, 0x10, 0x4a, 0x46, 0x49, 0x46, 0, 1 });
        Assert.False(FileSignatureValidator.IsSignatureValid("image/heic", stream));
    }
}

/// <summary>
/// A real 1600×800 HEIC image (plain sky blue, no content), generated once
/// with pillow-heif because Magick.NET can read HEIC but not write it.
/// </summary>
internal static class HeicFixture
{
    public const int Width = 1600;
    public const int Height = 800;

    private const string Base64 =
        "AAAAHGZ0eXBoZWljAAAAAG1pZjFoZWljbWlhZgAAAVdtZXRhAAAAAAAAACFoZGxyAAAAAAAAAABwaWN0AAAAAAAAAAAAAAAAAAAAACJpbG9jAAAAAERAAAEAAQAAAAABewABAAAAAAAAAXIAAAAjaWluZgAAAAAAAQAAABVpbmZlAgAAAAABAABodmMxAAAAAA5waXRtAAAAAAABAAAA12lwcnAAAAC4aXBjbwAAAHlodmNDAQNwAAAAkAAAAAAAePAA/P34+AAADwNgAAEAGEABDAH//wNwAAADAJAAAAMAAAMAeLoCQGEAAQAsQgEBA3AAAAMAkAAAAwAAAwB4oAMggDIWW6kkprm4CGgwIAAAAwMgAAADACFiAAEAB0QBwXKwYkAAAAATY29scm5jbHgAAQANAAaAAAAAFGlzcGUAAAAAAAAGQAAAAyAAAAAQcGl4aQAAAAADCAgIAAAAF2lwbWEAAAAAAAAAAQABBIECAwQAAAF6bWRhdAAAAW4oAa8ZGmuecbaaYYYYYXj3fIU3tPmssR0yG/wIFeKz/LGyjzqAcIXoxAAAAwAAAwAAAwAAAwAAAwAAAwADWqa3OoB6hWjyAAADAAADAAADAAADAAADAAADAAADAcOTBajgAAADAAADAAADAAADAAADAAADAAADAABYwIcAAAMAAAMAAAMAAAMAAAMAAAMAAAMAAAMABtwAAAMAAAMAAAMAAAMAAAMAAAMAAAMAAAMABhQAAAMAAAMAAAMAAAMAAAMAAAMAAAMAAAMAqoAAAAMAAAMAAAMAAAMAAAMAAAMAAAMAAAXUAAADAAADAAADAAADAAADAAADAAADAAAXUAAAAwAAAwAAAwAAAwAAAwAAAwAAAwAANuAAAAMAAAMAAAMAAAMAAAMAAAMAAAMAAHBAAAADAAADAAADAAADAAADAAADAAADAADBgAAAAwAAAwAAAwAAAwAAAwAAAwAAAwABXwAAAwAAAwAAAwAAj4A=";

    public static byte[] Bytes() => Convert.FromBase64String(Base64);
}
