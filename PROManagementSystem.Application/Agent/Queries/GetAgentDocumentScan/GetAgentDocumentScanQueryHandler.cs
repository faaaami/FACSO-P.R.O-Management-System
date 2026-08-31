using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocumentScan;

public sealed class GetAgentDocumentScanQueryHandler
    : IRequestHandler<
        GetAgentDocumentScanQuery,
        GetDocumentScanResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentScanQueryHandler(
        IApplicationDbContext context,
        IRenewalTaskReadRepository scopeRepository,
        ICurrentUserService currentUser)
    {
        _context = context;
        _scopeRepository = scopeRepository;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentScanResponseDto> Handle(
        GetAgentDocumentScanQuery request,
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

        var document =
            await _context.Documents
                .FirstOrDefaultAsync(
                    x => x.Id == request.DocumentId,
                    cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot access a deleted document.");
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
