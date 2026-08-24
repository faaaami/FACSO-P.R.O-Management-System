using MediatR;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;

public sealed record GetDocumentDependenciesQuery(
    Guid DocumentId
) : IRequest<GetDocumentDependenciesResponseDto>;