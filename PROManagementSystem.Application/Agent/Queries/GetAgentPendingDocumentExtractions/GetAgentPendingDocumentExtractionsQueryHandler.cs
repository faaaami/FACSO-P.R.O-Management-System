using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentPendingDocumentExtractions;

public sealed class GetAgentPendingDocumentExtractionsQueryHandler
    : IRequestHandler<
        GetAgentPendingDocumentExtractionsQuery,
        GetPendingDocumentExtractionsResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAgentPendingDocumentExtractionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetPendingDocumentExtractionsResponseDto> Handle(
        GetAgentPendingDocumentExtractionsQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        var status =
            DocumentExtractionStatusVocabulary.Resolve(request.Status);

        // Ownership scope: the agent's own drafts only. Legacy rows with a NULL
        // CreatedBy are included because confirmation still accepts them from any
        // in-scope agent (AgentConfirmExtractionCommandHandler), so listing them
        // is what keeps them reachable instead of stranded. The UI can tell them
        // apart from the agent's own rows because CreatedBy is null.
        var query = _context.DocumentExtractions
            .Where(x =>
                x.Status == status &&
                (x.CreatedBy == agentId || x.CreatedBy == null));

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
                x.FailureCode,
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
