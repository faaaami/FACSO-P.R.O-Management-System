using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentExtractionFile;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentExtractionFile;

public sealed class GetAgentDocumentExtractionFileQueryHandler
    : IRequestHandler<
        GetAgentDocumentExtractionFileQuery,
        ExtractionFileResult?>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    private static readonly HashSet<string> s_allowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

    public GetAgentDocumentExtractionFileQueryHandler(
        IApplicationDbContext context,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _context = context;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<ExtractionFileResult?> Handle(
        GetAgentDocumentExtractionFileQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        var extraction =
            await _context.DocumentExtractions
                .FirstOrDefaultAsync(
                    x => x.Id == request.ExtractionId,
                    cancellationToken);

        // Missing, soft-deleted, another agent's, or legacy NULL CreatedBy
        // rows are all indistinguishable (404). The same ownership rule as
        // the existing agent extraction GET endpoint.
        if (extraction is null ||
            extraction.IsDeleted ||
            extraction.CreatedBy != agentId)
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