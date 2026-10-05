using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentExtractionFile;

public sealed class GetDocumentExtractionFileQueryHandler
    : IRequestHandler<
        GetDocumentExtractionFileQuery,
        ExtractionFileResult?>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;

    private static readonly HashSet<string> s_allowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

    public GetDocumentExtractionFileQueryHandler(
        IApplicationDbContext context,
        IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<ExtractionFileResult?> Handle(
        GetDocumentExtractionFileQuery request,
        CancellationToken cancellationToken)
    {
        var extraction =
            await _context.DocumentExtractions
                .FirstOrDefaultAsync(
                    x => x.Id == request.ExtractionId,
                    cancellationToken);

        if (extraction is null || extraction.IsDeleted)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(extraction.FileUrl))
        {
            return null;
        }

        if (!s_allowedContentTypes.Contains(extraction.ContentType))
        {
            return null;
        }

        var stream = await _storage.ReadAsync(
            extraction.FileUrl,
            cancellationToken);

        if (stream is null)
        {
            return null;
        }

        var safeName = SanitizeFileName(extraction.FileName);

        return new ExtractionFileResult(
            stream,
            extraction.ContentType,
            safeName);
    }

    private static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "download";
        }

        // Strip every control character (including CR/LF) so the value can
        // never inject HTTP headers via Content-Disposition.
        var safeName = new string(
            fileName.Where(c => !char.IsControl(c)).ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(safeName)
            ? "download"
            : safeName;
    }
}
