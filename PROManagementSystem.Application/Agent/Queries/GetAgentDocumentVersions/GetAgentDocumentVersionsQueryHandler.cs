using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentVersions;

public sealed class GetAgentDocumentVersionsQueryHandler
    : IRequestHandler<
        GetAgentDocumentVersionsQuery,
        GetDocumentVersionsResponseDto>
{
    private readonly IDocumentVersionReadRepository _repository;
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentVersionsQueryHandler(
        IDocumentVersionReadRepository repository,
        IRenewalTaskReadRepository scopeRepository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _scopeRepository = scopeRepository;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentVersionsResponseDto> Handle(
        GetAgentDocumentVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // Strict task-linked scope check. Out-of-scope -> 404.
        var documentInScope =
            await _scopeRepository.IsAgentDocumentInScopeAsync(
                agentId,
                request.DocumentId,
                cancellationToken);

        if (!documentInScope)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        return await _repository.GetByDocumentIdAsync(
            request.DocumentId,
            false,
            cancellationToken);
    }
}
