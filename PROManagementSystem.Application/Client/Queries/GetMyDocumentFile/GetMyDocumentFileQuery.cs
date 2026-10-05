using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocumentFile;

/// <summary>
/// Client download of one of its own company's finalized documents.
/// Uses the same company scoping as the existing Client document detail and
/// scan endpoints — no new Client authorization surface.
/// </summary>
public sealed record GetMyDocumentFileQuery(
    Guid DocumentId
) : IRequest<DocumentFileResult?>;
