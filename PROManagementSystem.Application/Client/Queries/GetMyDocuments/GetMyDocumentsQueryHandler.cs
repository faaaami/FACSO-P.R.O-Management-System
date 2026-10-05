using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocuments;

public sealed class GetMyDocumentsQueryHandler
    : IRequestHandler<GetMyDocumentsQuery, GetClientDocumentsResponseDto>
{
    private readonly IDocumentReadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyDocumentsQueryHandler(
        IDocumentReadRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetClientDocumentsResponseDto> Handle(
        GetMyDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // Nonexistent or soft-deleted parent company -> 404, not an empty list.
        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x => x.Id == clientCompanyId.Value &&
                         !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // The tenant is enforced inside the SQL query for both entity-owned
        // and employee-owned documents combined in a single result set.
        return await _repository.GetByCompanyIdAsync(
            clientCompanyId.Value,
            request.Type,
            request.Status,
            request.ExpiresFrom,
            request.ExpiresTo,
            request.OwnerType,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}