using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

/// <summary>
/// Paged extraction-draft list for the Admin document scope. Defaults to the
/// "Pending" review queue.
/// </summary>
public sealed record GetPendingDocumentExtractionsQuery(
    int Page,
    int PageSize,
    string? Status = null
) : IRequest<GetPendingDocumentExtractionsResponseDto>;
