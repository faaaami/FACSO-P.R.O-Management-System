namespace PROManagementSystem.Application.Documents.Queries.GetDocumentExtractionFile;

/// <summary>
/// Returned by extraction file download handlers. The stream is a
/// seekable read-only file stream; the controller must dispose it
/// after streaming the HTTP response.
/// </summary>
public sealed record ExtractionFileResult(
    Stream Content,
    string ContentType,
    string FileName);
