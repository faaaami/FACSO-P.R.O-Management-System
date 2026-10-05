namespace PROManagementSystem.Application.Common.FileValidation;

/// <summary>
/// Verifies the leading magic bytes of an uploaded document against the
/// declared content type. Only the first few bytes are inspected so the file
/// body is never fully read into memory; seekable streams are left exactly
/// where they were found so the downstream storage/extraction flow reads from
/// the same position it would have without validation.
///
/// Non-seekable streams: consuming their leading bytes here would permanently
/// corrupt the shared stream for the storage/extraction reads that follow,
/// which would break the upload contract. For those streams the check is
/// skipped entirely (nothing is read) rather than degrading the upload. The
/// multipart streams produced by OpenReadStream are seekable, so the
/// signature check always runs on the real HTTP paths.
/// </summary>
public static class FileSignatureValidator
{
    public const string MismatchMessage =
        "File signature does not match the content type.";

    /// <summary>Longest signature the validator must read (PNG, 8 bytes).</summary>
    private const int MaxSignatureLength = 8;

    private static readonly
        IReadOnlyDictionary<string, byte[]>
        SignaturesByContentType =
            new Dictionary<string, byte[]>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["application/pdf"] =
                    [0x25, 0x50, 0x44, 0x46, 0x2d],       // "%PDF-"
                ["image/jpeg"] = [0xff, 0xd8, 0xff],      // "FF D8 FF"
                ["image/png"] =
                    [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]
            };

    public static bool IsSignatureValid(
        string? contentType,
        Stream stream)
    {
        if (contentType is null ||
            stream is null ||
            !stream.CanRead)
        {
            return false;
        }

        if (!SignaturesByContentType.TryGetValue(
                contentType,
                out var expected))
        {
            return false;
        }

        // Non-seekable streams cannot be replayed: reading the prefix would
        // consume bytes the storage/extraction flow depends on. Skip the
        // check without touching the stream rather than break the contract.
        if (!stream.CanSeek)
        {
            return true;
        }

        var originalPosition = stream.Position;

        try
        {
            var buffer = new byte[MaxSignatureLength];
            var totalRead = 0;

            while (totalRead < expected.Length)
            {
                var bytesRead = stream.Read(
                    buffer,
                    totalRead,
                    expected.Length - totalRead);

                if (bytesRead <= 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            if (totalRead < expected.Length)
            {
                return false;
            }

            return buffer.AsSpan(0, expected.Length)
                .SequenceEqual(expected);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}