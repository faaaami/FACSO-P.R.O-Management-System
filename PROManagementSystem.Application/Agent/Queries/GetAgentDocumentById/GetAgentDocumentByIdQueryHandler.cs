using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentById;

public sealed class GetAgentDocumentByIdQueryHandler
    : IRequestHandler<
        GetAgentDocumentByIdQuery,
        GetDocumentByIdResponseDto?>
{
    private readonly IDocumentReadRepository _repository;
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentByIdQueryHandler(
        IDocumentReadRepository repository,
        IRenewalTaskReadRepository scopeRepository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _scopeRepository = scopeRepository;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentByIdResponseDto?> Handle(
        GetAgentDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // Strict task-linked scope check. Out-of-scope -> null -> 404.
        var documentInScope =
            await _scopeRepository.IsAgentDocumentInScopeAsync(
                agentId,
                request.DocumentId,
                cancellationToken);

        if (!documentInScope)
        {
            return null;
        }

        return await _repository.GetByIdAsync(
            request.DocumentId,
            false,
            cancellationToken);
    }
}
