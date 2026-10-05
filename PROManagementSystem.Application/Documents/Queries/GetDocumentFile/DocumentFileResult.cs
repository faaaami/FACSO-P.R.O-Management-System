namespace PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

/// <summary>
/// Returned by confirmed-document file download handlers once the caller has
/// already passed its own authorization/scoping check. The stream is a
/// seekable read-only file stream; the controller must dispose it after
/// streaming the HTTP response.
/// </summary>
public sealed record DocumentFileResult(
    Stream Content,
    string ContentType,
    string FileName);
