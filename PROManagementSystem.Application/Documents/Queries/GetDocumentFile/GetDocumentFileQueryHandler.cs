using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentFile;

public sealed class GetDocumentFileQueryHandler
    : IRequestHandler<
        GetDocumentFileQuery,
        DocumentFileResult?>
{
    private readonly IDocumentReadRepository _repository;
    private readonly IFileStorageService _storage;

    public GetDocumentFileQueryHandler(
        IDocumentReadRepository repository,
        IFileStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<DocumentFileResult?> Handle(
        GetDocumentFileQuery request,
        CancellationToken cancellationToken)
    {
        // includeDeleted: false — a soft-deleted document's file is
        // unreachable through this API, exactly as for the existing scan
        // endpoint. Missing or deleted -> null -> 404.
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
