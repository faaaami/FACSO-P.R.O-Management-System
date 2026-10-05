using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Client.Queries.GetMyExpiringDocuments;

public sealed class GetMyExpiringDocumentsQueryHandler
    : IRequestHandler<
        GetMyExpiringDocumentsQuery,
        GetExpiringDocumentsResponseDto>
{
    private readonly IDocumentExpiryReadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyExpiringDocumentsQueryHandler(
        IDocumentExpiryReadRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetExpiringDocumentsResponseDto> Handle(
        GetMyExpiringDocumentsQuery request,
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

        // The tenant is enforced inside the SQL query.
        return await _repository.GetExpiringByCompanyIdAsync(
            clientCompanyId.Value,
            request.Days,
            request.Page,
            request.PageSize,
            request.IncludeExpired,
            cancellationToken);
    }
}