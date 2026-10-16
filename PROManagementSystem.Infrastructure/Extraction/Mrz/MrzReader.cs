namespace PROManagementSystem.Infrastructure.Extraction.Mrz;

/// <summary>
/// Finds and reads the MRZ in a prepared (upright, grayscale) image. OCR is
/// tried both restricted to MRZ characters and unrestricted (the two make
/// different mistakes, e.g. L read as 1). Regions, cheapest first: the bottom
/// of the image, the whole image, then the image rotated 180°, 90° and 270°
/// for photos taken upside down or sideways without EXIF orientation. Stops
/// at the first zone whose check digits all pass; otherwise returns the read
/// that passes the most check digits (or null).
/// </summary>
public sealed class MrzReader
{
    // The MRZ sits in the bottom quarter of a passport photo page and the
    // bottom ~40% of an ID-1 card; 45% covers both with some framing margin.
    private const double BottomFraction = 0.45;

    private static readonly (int Degrees, bool BottomOnly, bool Whitelist)[] Attempts =
    {
        (0, true, true),
        (0, true, false),
        (0, false, true),
        (0, false, false),
        (180, true, true),
        (180, true, false),
        (90, false, true),
        (270, false, true)
    };

    private static readonly OcrRecognitionOptions PlainBlock = new(SingleBlock: true);

    private readonly IImageOcrExtractor _ocr;
    private readonly IImagePreprocessor _images;

    public MrzReader(IImageOcrExtractor ocr, IImagePreprocessor images)
    {
        _ocr = ocr;
        _images = images;
    }

    public async Task<MrzResult?> ReadAsync(
        PreparedImage image,
        MrzFormat format,
        CancellationToken cancellationToken)
    {
        if (image.Png.Length == 0)
        {
            return null;
        }

        MrzResult? best = null;

        foreach (var (degrees, bottomOnly, whitelist) in Attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var png = degrees == 0 ? image.Png : _images.Rotate(image.Png, degrees);

            if (bottomOnly)
            {
                png = _images.CropBottom(png, BottomFraction);
            }

            await using var stream = new MemoryStream(png, writable: false);

            var ocr = await _ocr.ExtractAsync(
                stream,
                whitelist ? OcrRecognitionOptions.Mrz : PlainBlock,
                cancellationToken);

            if (!ocr.HasUsableText)
            {
                continue;
            }

            var result = MrzParser.FindAndParse(ocr.Text, format);

            if (result is null)
            {
                continue;
            }

            best = MrzParser.Prefer(best, result);

            if (best.AllChecksValid)
            {
                return best;
            }
        }

        return best;
    }
}
