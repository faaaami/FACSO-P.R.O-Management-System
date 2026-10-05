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
}

/// <summary>
/// The text recognised from a raster image. <see cref="HasUsableText"/> is
/// <see langword="true"/> only when Tesseract produced non-whitespace text.
/// </summary>
public sealed record OcrTextExtractionResult(
    string Text,
    bool HasUsableText);