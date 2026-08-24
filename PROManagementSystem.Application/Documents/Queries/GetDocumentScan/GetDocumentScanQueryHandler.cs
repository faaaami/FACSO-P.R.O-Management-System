using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentScan;

public sealed class GetDocumentScanQueryHandler
    : IRequestHandler<
        GetDocumentScanQuery,
        GetDocumentScanResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentScanQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetDocumentScanResponseDto> Handle(
        GetDocumentScanQuery request,
        CancellationToken cancellationToken)
    {
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