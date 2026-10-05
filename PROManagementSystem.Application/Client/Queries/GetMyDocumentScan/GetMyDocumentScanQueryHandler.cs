using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocumentScan;

public sealed class GetMyDocumentScanQueryHandler
    : IRequestHandler<GetMyDocumentScanQuery, GetDocumentScanResponseDto>
{
    private readonly IDocumentReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyDocumentScanQueryHandler(
        IDocumentReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentScanResponseDto> Handle(
        GetMyDocumentScanQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // Company-scoped lookup: out-of-company or nonexistent document -> 404.
        var document = await _repository.GetByIdForCompanyAsync(
            clientCompanyId.Value,
            request.DocumentId,
            cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        if (string.IsNullOrWhiteSpace(document.FileUrl))
        {
            throw new InvalidOperationException(
                "Document does not have an associated file.");
        }

        return new GetDocumentScanResponseDto(
            document.Id,
            document.FileUrl,
            document.FileName,
            document.ContentType);
    }
}