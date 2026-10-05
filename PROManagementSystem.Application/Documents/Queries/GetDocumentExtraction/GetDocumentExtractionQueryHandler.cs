using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentExtraction;

public sealed class GetDocumentExtractionQueryHandler
    : IRequestHandler<
        GetDocumentExtractionQuery,
        GetDocumentExtractionResponseDto?>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentExtractionQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetDocumentExtractionResponseDto?> Handle(
        GetDocumentExtractionQuery request,
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

        return new GetDocumentExtractionResponseDto(
            extraction.Id,
            extraction.FileName,
            extraction.ContentType,
            extraction.FileSize,
            extraction.SuggestedType,
            extraction.SuggestedDocumentNumber,
            extraction.SuggestedIssueDate,
            extraction.SuggestedExpiryDate,
            extraction.SuggestedOwnerType,
            extraction.Confidence,
            extraction.Status,
            extraction.ExpiresAt,
            DetailsJson.FromStorage(extraction.SuggestedDetails));
    }
}