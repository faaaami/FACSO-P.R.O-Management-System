using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocumentFile;

public sealed class GetMyDocumentFileQueryHandler
    : IRequestHandler<
        GetMyDocumentFileQuery,
        DocumentFileResult?>
{
    private readonly IDocumentReadRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public GetMyDocumentFileQueryHandler(
        IDocumentReadRepository repository,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<DocumentFileResult?> Handle(
        GetMyDocumentFileQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // The tenant restriction runs inside the SQL query, so an
        // out-of-company or nonexistent document resolves to null -> 404.
        // This is the identical lookup the Client document detail endpoint uses.
        var document = await _repository.GetByIdForCompanyAsync(
            clientCompanyId.Value,
            request.DocumentId,
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
