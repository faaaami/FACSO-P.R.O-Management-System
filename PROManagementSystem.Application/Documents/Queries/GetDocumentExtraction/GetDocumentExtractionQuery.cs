using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentExtraction;

public sealed record GetDocumentExtractionQuery(
    Guid ExtractionId
) : IRequest<GetDocumentExtractionResponseDto?>;