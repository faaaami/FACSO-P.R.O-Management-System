using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentFile;

public sealed class GetAgentDocumentFileQueryHandler
    : IRequestHandler<
        GetAgentDocumentFileQuery,
        DocumentFileResult?>
{
    private readonly IDocumentReadRepository _repository;
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentFileQueryHandler(
        IDocumentReadRepository repository,
        IRenewalTaskReadRepository scopeRepository,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _scopeRepository = scopeRepository;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<DocumentFileResult?> Handle(
        GetAgentDocumentFileQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // Same strict task-linked scope check as the agent document detail
        // endpoint. Out-of-scope -> null -> 404, so an agent cannot use this
        // route to probe for documents outside their portfolio.
        var documentInScope =
            await _scopeRepository.IsAgentDocumentInScopeAsync(
                agentId,
                request.DocumentId,
                cancellationToken);

        if (!documentInScope)
        {
            return null;
        }

        var document = await _repository.GetByIdAsync(
            request.DocumentId,
            false,
            cancellationToken);

        if (document is null)
        {
            return null;
        }

        return await DocumentFileContentResolver.TryResolveAsync(
            _storage,
            document.FileUrl,
            document.ContentType,
            document.FileName,
            cancellationToken);
    }
}
