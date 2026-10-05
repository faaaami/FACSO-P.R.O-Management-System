using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Client.Queries.GetMyContacts;

public sealed class GetMyContactsQueryHandler
    : IRequestHandler<GetMyContactsQuery, GetClientContactsResponseDto>
{
    private readonly IClientContactReadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyContactsQueryHandler(
        IClientContactReadRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetClientContactsResponseDto> Handle(
        GetMyContactsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ArgumentException(
                "Page must be greater than or equal to 1.");
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ArgumentException(
                "PageSize must be between 1 and 100.");
        }

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

        return await _repository.GetPagedAsync(
            clientCompanyId.Value,
            request.Page,
            request.PageSize,
            request.Search,
            includeDeleted: false,
            cancellationToken);
    }
}