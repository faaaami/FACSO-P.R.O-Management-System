using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentExtractionFile;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentExtractionFile;

public sealed record GetAgentDocumentExtractionFileQuery(
    Guid ExtractionId
) : IRequest<ExtractionFileResult?>;