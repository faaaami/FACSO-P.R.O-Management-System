namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Extracts the embedded text layer from a selectable PDF using PdfPig.
/// Scanned PDFs carry no text layer and resolve to
/// <see cref="PdfTextExtractionResult.HasUsableText"/> equal to
/// <see langword="false"/>.
/// </summary>
public interface IPdfTextExtractor
{
    Task<PdfTextExtractionResult> ExtractAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The text layer extracted from a PDF. <see cref="HasUsableText"/> is
/// <see langword="true"/> only when at least one page contained
/// non-whitespace text.
/// </summary>
public sealed record PdfTextExtractionResult(
    string Text,
    bool HasUsableText);