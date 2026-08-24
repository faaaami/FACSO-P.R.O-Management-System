using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentById;

public sealed class GetDocumentByIdQueryHandler
    : IRequestHandler<
        GetDocumentByIdQuery,
        GetDocumentByIdResponseDto?>
{
    private readonly IDocumentReadRepository _repository;

    public GetDocumentByIdQueryHandler(
        IDocumentReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentByIdResponseDto?> Handle(
        GetDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(
            request.DocumentId,
            request.IncludeDeleted,
            cancellationToken);
    }
}