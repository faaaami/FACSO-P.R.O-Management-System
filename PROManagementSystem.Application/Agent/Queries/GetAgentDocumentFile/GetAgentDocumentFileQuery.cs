using MediatR;
using PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentFile;

/// <summary>
/// Agent download of a confirmed document's stored file. Scoped to the
/// agent's task-linked document portfolio.
/// </summary>
public sealed record GetAgentDocumentFileQuery(
    Guid DocumentId
) : IRequest<DocumentFileResult?>;
