using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

public sealed class GetPendingDocumentExtractionsQueryHandler
    : IRequestHandler<
        GetPendingDocumentExtractionsQuery,
        GetPendingDocumentExtractionsResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetPendingDocumentExtractionsQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetPendingDocumentExtractionsResponseDto> Handle(
        GetPendingDocumentExtractionsQuery request,
        CancellationToken cancellationToken)
    {
        var status =
            DocumentExtractionStatusVocabulary.Resolve(request.Status);

        // Soft-deleted drafts are excluded by the global query filter; the
        // status is matched case-insensitively against the stored literal.
        var query = _context.DocumentExtractions
            .Where(x => x.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new PendingDocumentExtractionListItemDto(
                x.Id,
                x.FileName,
                x.ContentType,
                x.FileSize,
                x.SuggestedType,
                x.SuggestedDocumentNumber,
                x.SuggestedIssueDate,
                x.SuggestedExpiryDate,
                x.SuggestedOwnerType,
                x.Confidence,
                x.Status,
                x.ExpiresAt,
                x.CreatedAt,
                x.CreatedBy))
            .ToListAsync(cancellationToken);

        return new GetPendingDocumentExtractionsResponseDto(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }
}
