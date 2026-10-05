namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Stores uploaded file content outside the database and returns a
/// server-generated relative storage reference (never an absolute
/// filesystem path or a public URL).
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens a previously saved file for reading. Returns a seekable
    /// read-only stream that the caller must dispose. Returns
    /// <c>null</c> if the reference is empty or the physical file
    /// does not exist.
    /// </summary>
    Task<Stream?> ReadAsync(
        string reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort removal of a previously saved reference. Used to clean
    /// up orphaned files when a later step in the same request fails.
    /// </summary>
    Task DeleteAsync(
        string reference,
        CancellationToken cancellationToken);
}