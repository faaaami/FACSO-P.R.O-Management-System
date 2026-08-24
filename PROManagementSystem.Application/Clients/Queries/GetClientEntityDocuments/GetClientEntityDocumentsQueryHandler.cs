using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;

public sealed class GetClientEntityDocumentsQueryHandler
    : IRequestHandler<
        GetClientEntityDocumentsQuery,
        GetClientEntityDocumentsResponseDto>
{
    private readonly IClientEntityDocumentReadRepository _repository;

    public GetClientEntityDocumentsQueryHandler(
        IClientEntityDocumentReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetClientEntityDocumentsResponseDto> Handle(
        GetClientEntityDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByClientEntityIdAsync(
            request.ClientEntityId,
            request.IncludeDeleted,
            cancellationToken);
    }
}