using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

public sealed class GetDocumentVersionsQueryHandler
    : IRequestHandler<
        GetDocumentVersionsQuery,
        GetDocumentVersionsResponseDto>
{
    private readonly IDocumentVersionReadRepository _repository;

    public GetDocumentVersionsQueryHandler(
        IDocumentVersionReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentVersionsResponseDto> Handle(
        GetDocumentVersionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByDocumentIdAsync(
            request.DocumentId,
            request.IncludeDeleted,
            cancellationToken);
    }
}