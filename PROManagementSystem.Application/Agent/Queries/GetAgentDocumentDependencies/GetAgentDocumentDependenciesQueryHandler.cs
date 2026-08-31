using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentDependencies;

public sealed class GetAgentDocumentDependenciesQueryHandler
    : IRequestHandler<
        GetAgentDocumentDependenciesQuery,
        GetDocumentDependenciesResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentDependenciesQueryHandler(
        IApplicationDbContext context,
        IRenewalTaskReadRepository scopeRepository,
        ICurrentUserService currentUser)
    {
        _context = context;
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

        // 2. Only dependency documents that are also within the
        //    agent's scope may be returned. Never expose
        //    out-of-scope dependencies.
        var scopedDocumentIds =
            await _scopeRepository.GetAgentScopedDocumentIdsAsync(
                agentId,
                cancellationToken);

        // 3. Get dependencies restricted to the agent's scope
        var items =
            await (
                from dependency in _context.DocumentDependencies

                join requiredDocument
                    in _context.Documents
                    on dependency.DependsOnDocumentId
                    equals requiredDocument.Id

                where dependency.DocumentId == request.DocumentId
                      && !dependency.IsDeleted
                      && !requiredDocument.IsDeleted
                      && scopedDocumentIds.Contains(requiredDocument.Id)

                orderby dependency.CreatedAt descending

                select new DocumentDependencyItemDto(
                    dependency.Id,
                    dependency.DependsOnDocumentId,
                    requiredDocument.Type,
                    requiredDocument.DocumentNumber,
                    requiredDocument.ExpiryDate,
                    requiredDocument.IsActive,
                    requiredDocument.IsDeleted
                ))
            .ToListAsync(cancellationToken);

        return new GetDocumentDependenciesResponseDto(
            request.DocumentId,
            items,
            items.Count);
    }
}
