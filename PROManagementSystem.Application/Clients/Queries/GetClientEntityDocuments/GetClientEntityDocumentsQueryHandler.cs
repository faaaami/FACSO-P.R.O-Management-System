using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;

public sealed class GetClientEntityDocumentsQueryHandler
    : IRequestHandler<
        GetClientEntityDocumentsQuery,
        GetClientEntityDocumentsResponseDto>
{
    private readonly IClientEntityDocumentReadRepository _repository;
    private readonly IApplicationDbContext _context;

    public GetClientEntityDocumentsQueryHandler(
        IClientEntityDocumentReadRepository repository,
        IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<GetClientEntityDocumentsResponseDto> Handle(
        GetClientEntityDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        // Nonexistent or soft-deleted parent entity -> 404, not an empty list.
        var entityExists =
            await _context.ClientEntities
                .AnyAsync(
                    x => x.Id == request.ClientEntityId &&
                         !x.IsDeleted,
                    cancellationToken);

        if (!entityExists)
        {
            throw new KeyNotFoundException(
                "Client entity was not found.");
        }

        return await _repository.GetByClientEntityIdAsync(
            request.ClientEntityId,
            request.IncludeDeleted,
            cancellationToken);
    }
}