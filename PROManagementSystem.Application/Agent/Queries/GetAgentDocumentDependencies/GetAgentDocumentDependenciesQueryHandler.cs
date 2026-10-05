using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentDependencies;

public sealed class GetAgentDocumentDependenciesQueryHandler
    : IRequestHandler<
        GetAgentDocumentDependenciesQuery,
        GetDocumentDependenciesResponseDto>
{
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentDependenciesQueryHandler(
        IRenewalTaskReadRepository scopeRepository,
        ICurrentUserService currentUser)
    {
        _scopeRepository = scopeRepository;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentDependenciesResponseDto> Handle(
        GetAgentDocumentDependenciesQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // 1. Verify the parent document is within the agent's
        //    strict task-linked scope. Out-of-scope -> 404.
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

        // 2. Get dependencies restricted to the agent's scope (the scope
        //    predicate runs in SQL, so no in-memory ID set is materialized).
        return await _scopeRepository.GetAgentDocumentDependenciesAsync(
            agentId,
            request.DocumentId,
            cancellationToken);
    }
}
