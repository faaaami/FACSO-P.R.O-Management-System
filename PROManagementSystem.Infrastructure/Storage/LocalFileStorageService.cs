using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files under the configured storage root and returns a
/// server-generated relative reference. The client-supplied file name is
/// never used to build a path - references are composed from a GUID key and
/// a canonical extension resolved from the validated content type, so
/// traversal/absolute/UNC file names can never escape the storage root.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private const string StorageFolderName = "extractions";

    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<StorageOptions> options)
    {
        var configuredRoot =
            string.IsNullOrWhiteSpace(options.Value.RootPath)
                ? "storage"
                : options.Value.RootPath;

        _rootPath = Path.GetFullPath(configuredRoot);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var extension = ResolveExtension(fileName, contentType);

        var reference =
            $"{StorageFolderName}/{Guid.NewGuid():N}{extension}";

        var fullPath = ResolvePhysicalPath(reference);

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)!);

        // FileMode.CreateNew never overwrites an existing key.
        await using var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(
            fileStream,
            cancellationToken);

        return reference;
    }

    public Task<Stream?> ReadAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult<Stream?>(null);
        }

        string fullPath;

        try
        {
            fullPath = ResolvePhysicalPath(reference);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<Stream?>(null);
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.CompletedTask;
        }

        var fullPath = ResolvePhysicalPath(reference);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps a validated content type to a canonical extension, falling back
    /// to a sanitized, length-clamped extension taken from the file name.
    /// </summary>
    private static string ResolveExtension(
        string fileName,
        string contentType)
    {
        var extension = contentType switch
        {
            "application/pdf" => "pdf",
            "image/jpeg" => "jpg",
            "image/png" => "png",
            _ => null
        };

        if (extension is null)
        {
            extension = GetExtension(fileName);
        }

        return extension is null || extension.Length > 10
            ? string.Empty
            : $".{extension}";
    }

    private static string? GetExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var safeName = Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(safeName))
        {
            return null;
        }

        var dotIndex = safeName.LastIndexOf('.');
        if (dotIndex < 0 || dotIndex == safeName.Length - 1)
        {
            return null;
        }

        var extension = safeName[(dotIndex + 1)..];
        return string.IsNullOrWhiteSpace(extension)
            ? null
            : extension;
    }

    /// <summary>
    /// Rejects rooted/UNC/traversal references and confirms the resolved
    /// absolute path stays inside the configured storage root.
    /// </summary>
    private string ResolvePhysicalPath(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "Storage reference must not be empty.",
                nameof(reference));
        }

        if (Path.IsPathRooted(reference) ||
            reference.StartsWith("\\\\", StringComparison.Ordinal) ||
            reference.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Storage reference must be a relative path.",
                nameof(reference));
        }

        if (reference.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Storage reference must not contain parent traversal.",
                nameof(reference));
        }

        var normalized = reference.Replace(
            '/',
            Path.DirectorySeparatorChar);

        var candidate = Path.GetFullPath(
            Path.Combine(_rootPath, normalized));

        var rootWithSeparator = _rootPath.EndsWith(
            Path.DirectorySeparatorChar)
                ? _rootPath
                : _rootPath + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
                rootWithSeparator,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Storage reference resolves outside the storage root.",
                nameof(reference));
        }

        return candidate;
    }
}