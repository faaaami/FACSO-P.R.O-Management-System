using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

/// <summary>
/// Admin download of a confirmed document's stored file.
/// </summary>
public sealed record GetDocumentFileQuery(
    Guid DocumentId
) : IRequest<DocumentFileResult?>;
