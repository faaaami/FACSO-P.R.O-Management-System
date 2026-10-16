namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Runs local Tesseract OCR over a raster document image. JPEG and PNG scans
/// are decoded in memory and recognised for their text layer; the resulting
/// text is internal to the extraction pipeline and never surfaced in the API.
/// </summary>
public interface IImageOcrExtractor
{
    Task<OcrTextExtractionResult> ExtractAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// OCR with recognition options (e.g. MRZ: a character whitelist and a
    /// single text block). Implementations without option support fall back
    /// to plain recognition.
    /// </summary>
    Task<OcrTextExtractionResult> ExtractAsync(
        Stream imageStream,
        OcrRecognitionOptions options,
        CancellationToken cancellationToken = default)
        => ExtractAsync(imageStream, cancellationToken);
}

/// <summary>
/// The text recognised from a raster image. <see cref="HasUsableText"/> is
/// <see langword="true"/> only when Tesseract produced non-whitespace text.
/// </summary>
public sealed record OcrTextExtractionResult(
    string Text,
    bool HasUsableText);

/// <param name="CharacterWhitelist">Only these characters are recognised.</param>
/// <param name="SingleBlock">Treat the image as one uniform block of text.</param>
public sealed record OcrRecognitionOptions(
    string? CharacterWhitelist = null,
    bool SingleBlock = false)
{
    public static readonly OcrRecognitionOptions Mrz =
        new("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<", SingleBlock: true);
}
