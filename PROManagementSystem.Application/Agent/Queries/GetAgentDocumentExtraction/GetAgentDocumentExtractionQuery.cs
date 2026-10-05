using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentExtraction;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentExtraction;

public sealed record GetAgentDocumentExtractionQuery(
    Guid ExtractionId
) : IRequest<GetDocumentExtractionResponseDto?>;