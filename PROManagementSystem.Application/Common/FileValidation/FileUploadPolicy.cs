namespace PROManagementSystem.Application.Common.FileValidation;

public static class FileUploadPolicy
{
    public const long MinFileSizeBytes = 1;
    public const long MaxFileSizeBytes = 20L * 1024 * 1024;
    public const int MaxFileNameLength = 255;

    /// <summary>
    /// Allowed extension per content type. Key/value comparisons are
    /// case-insensitive so ".PDF" + "application/pdf" is accepted.
    /// </summary>
    private static readonly
        IReadOnlyDictionary<string, IReadOnlySet<string>>
        AllowedExtensionsByContentType =
            new Dictionary<string, IReadOnlySet<string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["application/pdf"] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "pdf"
                    },
                ["image/jpeg"] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "jpg",
                        "jpeg"
                    },
                ["image/png"] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "png"
                    },
                // iPhone photos. Converted to JPEG before storage so they can
                // be previewed in the browser.
                ["image/heic"] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "heic",
                        "heif"
                    },
                ["image/heif"] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "heif",
                        "heic"
                    }
            };

    public static bool IsHeic(string? contentType)
        => string.Equals(contentType, "image/heic", StringComparison.OrdinalIgnoreCase)
           || string.Equals(contentType, "image/heif", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Browsers on Windows and Android often send HEIC photos as
    /// application/octet-stream or with no type. For a .heic/.heif file name
    /// the type is taken from the extension; every other type is returned
    /// unchanged (and still checked against the file's magic bytes).
    /// </summary>
    public static string NormalizeContentType(string? contentType, string? fileName)
    {
        var extension = string.IsNullOrWhiteSpace(fileName)
            ? null
            : GetExtension(fileName);

        var isGeneric = string.IsNullOrWhiteSpace(contentType)
            || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);

        if (isGeneric && extension is not null)
        {
            if (extension.Equals("heic", StringComparison.OrdinalIgnoreCase))
            {
                return "image/heic";
            }

            if (extension.Equals("heif", StringComparison.OrdinalIgnoreCase))
            {
                return "image/heif";
            }
        }

        return contentType ?? string.Empty;
    }

    public static bool IsContentTypeAllowed(string? contentType)
        => contentType is not null
           && AllowedExtensionsByContentType.ContainsKey(contentType);

    public static bool IsExtensionAllowed(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = GetExtension(fileName);
        return extension is not null
               && AllowedExtensionsByContentType.Values.Any(
                   allowed => allowed.Contains(extension));
    }

    public static bool IsMimeExtensionPairValid(
        string? contentType,
        string? fileName)
    {
        if (contentType is null || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = GetExtension(fileName);
        return extension is not null
               && AllowedExtensionsByContentType.TryGetValue(
                   contentType,
                   out var allowedExtensions)
               && allowedExtensions.Contains(extension);
    }

    /// <summary>
    /// Returns the file extension without the leading dot, or null when the
    /// file name has no extension. Uses LastIndexOf so arbitrary client
    /// file names cannot throw Path API exceptions.
    /// </summary>
    private static string? GetExtension(string fileName)
    {
        var separatorIndex = fileName.LastIndexOf('.');
        if (separatorIndex < 0 ||
            separatorIndex == fileName.Length - 1)
        {
            return null;
        }

        return fileName[(separatorIndex + 1)..];
    }
}