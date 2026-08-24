using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

public sealed class GetExpiringDocumentsQueryHandler
    : IRequestHandler<
        GetExpiringDocumentsQuery,
        GetExpiringDocumentsResponseDto>
{
    private readonly IDocumentExpiryReadRepository _repository;

    public GetExpiringDocumentsQueryHandler(
        IDocumentExpiryReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetExpiringDocumentsResponseDto> Handle(
        GetExpiringDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetExpiringAsync(
            request.Days,
            request.Page,
            request.PageSize,
            request.IncludeExpired,
            cancellationToken);
    }
}