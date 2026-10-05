using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

/// <summary>
/// Shared resolution step for confirmed-document file downloads, applied after
/// the caller-specific authorization/scoping check has resolved the Document.
///
/// This mirrors the safety rules the existing extraction-file download already
/// enforces, so a confirmed document's bytes are served no less defensively
/// than a draft's:
///   - a blank stored reference yields null (no file attached);
///   - only the same three upload content types are ever streamed;
///   - the physical read goes through IFileStorageService, which rejects
///     rooted/UNC/traversal references and confirms the resolved path stays
///     inside the storage root (an invalid reference reads as null, never as
///     bytes from outside the root);
///   - the download name is stripped of every control character, so it can
///     never inject a Content-Disposition header.
///
/// The storage reference itself is never returned, so no physical path or
/// internal key reaches the browser.
/// </summary>
internal static class DocumentFileContentResolver
{
    private static readonly HashSet<string> s_allowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

    public static async Task<DocumentFileResult?> TryResolveAsync(
        IFileStorageService storage,
        string? fileUrl,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) ||
            string.IsNullOrWhiteSpace(contentType) ||
            !s_allowedContentTypes.Contains(contentType))
        {
            return null;
        }

        var stream = await storage.ReadAsync(
            fileUrl,
            cancellationToken);

        if (stream is null)
        {
            return null;
        }

        return new DocumentFileResult(
            stream,
            contentType,
            SanitizeFileName(fileName));
    }

    private static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "download";
        }

        var safeName = new string(
            fileName.Where(c => !char.IsControl(c)).ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(safeName)
            ? "download"
            : safeName;
    }
}
