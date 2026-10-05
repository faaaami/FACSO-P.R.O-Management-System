using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntities;

public sealed class GetClientEntitiesQueryHandler
    : IRequestHandler<
        GetClientEntitiesQuery,
        GetClientEntitiesResponseDto>
{
    private readonly IClientEntityReadRepository _repository;
    private readonly IApplicationDbContext _context;

    public GetClientEntitiesQueryHandler(
        IClientEntityReadRepository repository,
        IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<GetClientEntitiesResponseDto> Handle(
        GetClientEntitiesQuery request,
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

        // Nonexistent or soft-deleted parent company -> 404, not an empty list.
        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x => x.Id == request.ClientCompanyId &&
                         !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _repository.GetPagedAsync(
            request.ClientCompanyId,
            request.Page,
            request.PageSize,
            request.Search,
            request.IncludeDeleted,
            cancellationToken);
    }
}