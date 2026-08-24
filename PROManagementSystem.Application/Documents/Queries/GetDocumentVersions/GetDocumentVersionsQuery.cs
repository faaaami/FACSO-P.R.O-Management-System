using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

public sealed record GetDocumentVersionsQuery(
    Guid DocumentId,
    bool IncludeDeleted = false
) : IRequest<GetDocumentVersionsResponseDto>;