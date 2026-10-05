using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentExtractionFile;

public sealed record GetDocumentExtractionFileQuery(
    Guid ExtractionId
) : IRequest<ExtractionFileResult?>;
