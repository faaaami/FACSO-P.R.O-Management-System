using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetPendingDocumentExtractions;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentPendingDocumentExtractions;

/// <summary>
/// Paged extraction-draft list for the signed-in Agent.
/// </summary>
public sealed record GetAgentPendingDocumentExtractionsQuery(
    int Page,
    int PageSize,
    string? Status = null
) : IRequest<GetPendingDocumentExtractionsResponseDto>;
