using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Reads suggested values from an uploaded document of a type chosen by
/// staff. Suggestions only pre-fill the review form; nothing is saved until a
/// person confirms.
/// </summary>
public interface IDocumentExtractionService
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken);
}

public sealed record DocumentExtractionRequest(
    Stream Content,
    string ContentType,
    DocumentType DocumentType);

/// <summary>Where a suggested value came from.</summary>
public enum ExtractedFieldSource
{
    /// <summary>Not read from the document; the reviewer enters it.</summary>
    Manual = 0,

    /// <summary>Machine-readable zone, check-digit verified per field.</summary>
    Mrz = 1,

    /// <summary>Recognised text (OCR or a PDF text layer).</summary>
    Ocr = 2,

    /// <summary>AI extraction (only when enabled in settings).</summary>
    Ai = 3
}

/// <summary>
/// A suggested value. Dates are yyyy-MM-dd. Errors mean the value is known to
/// be wrong or unreadable; warnings mean it needs checking.
/// </summary>
public sealed record ExtractedField(
    string? Value,
    ExtractedFieldSource Source,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static ExtractedField Read(
        string? value,
        ExtractedFieldSource source,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null)
        => new(value, source, errors ?? Array.Empty<string>(), warnings ?? Array.Empty<string>());
}

/// <summary>Why nothing (or not everything) could be read, for the user.</summary>
public sealed record ExtractionFailure(string Code, string Message);

/// <summary>
/// Field keys are "documentNumber", "issueDate", "expiryDate" and the
/// document type's schema keys (see IDocumentTypeSchemaProvider).
/// </summary>
public sealed record DocumentExtractionResult(
    DocumentType Type,
    IReadOnlyDictionary<string, ExtractedField> Fields,
    ExtractionFailure? Failure)
{
    public const string DocumentNumberKey = "documentNumber";
    public const string IssueDateKey = "issueDate";
    public const string ExpiryDateKey = "expiryDate";

    public static DocumentExtractionResult Empty(DocumentType type, ExtractionFailure? failure = null)
        => new(type, new Dictionary<string, ExtractedField>(), failure);
}
