namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>An upload as it will be stored.</summary>
public sealed record StoredUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSize) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Converts uploads browsers cannot display (HEIC/HEIF photos) to JPEG before
/// they are stored, applying the EXIF orientation. Other files pass through.
/// </summary>
public interface IUploadImageConverter
{
    /// <summary>
    /// Returns null when the file needs no conversion. Throws
    /// <see cref="InvalidOperationException"/> when a HEIC file cannot be
    /// decoded.
    /// </summary>
    Task<StoredUpload?> ConvertForStorageAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);
}
