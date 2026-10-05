using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentExtraction;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentExtraction;

public sealed class GetAgentDocumentExtractionQueryHandler
    : IRequestHandler<
        GetAgentDocumentExtractionQuery,
        GetDocumentExtractionResponseDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentExtractionQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentExtractionResponseDto?> Handle(
        GetAgentDocumentExtractionQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        var extraction =
            await _context.DocumentExtractions
                .FirstOrDefaultAsync(
                    x => x.Id == request.ExtractionId,
                    cancellationToken);

        // Retrieval is owned by the creating agent only. Missing, soft-deleted,
        // another agent's, or legacy NULL CreatedBy rows are all indistinguishable
        // (404). This is intentionally stricter than confirmation, which keeps the
        // pre-H1 legacy behavior for NULL CreatedBy rows.
        if (extraction is null ||
            extraction.IsDeleted ||
            extraction.CreatedBy != agentId)
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