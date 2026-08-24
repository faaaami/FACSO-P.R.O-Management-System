using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;

public sealed class GetDocumentDependenciesQueryHandler
    : IRequestHandler<
        GetDocumentDependenciesQuery,
        GetDocumentDependenciesResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentDependenciesQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetDocumentDependenciesResponseDto> Handle(
        GetDocumentDependenciesQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Verify the document exists
        var documentExists =
            await _context.Documents
                .AnyAsync(
                    x => x.Id == request.DocumentId,
                    cancellationToken);

        if (!documentExists)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        // 2. Get active dependencies
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