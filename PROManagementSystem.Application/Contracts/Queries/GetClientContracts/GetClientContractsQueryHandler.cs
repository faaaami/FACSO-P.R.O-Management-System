using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Contracts.Queries.GetClientContracts;

public sealed class GetClientContractsQueryHandler
    : IRequestHandler<
        GetClientContractsQuery,
        GetClientContractsResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetClientContractsQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetClientContractsResponseDto> Handle(
        GetClientContractsQuery request,
        CancellationToken cancellationToken)
    {
        var query =
            _context.ServiceContracts
                .AsNoTracking()
                .Where(
                    x =>
                        x.ClientCompanyId ==
                            request.ClientCompanyId &&
                        !x.IsDeleted);

        var offset = (request.Page - 1) * request.PageSize;

        var items =
            await query
                .OrderByDescending(x => x.StartDate)
                .ThenBy(x => x.Id)
                .Select(
                    x =>
                        new ServiceContractListItemDto(
                            x.Id,
                            x.ClientCompanyId,
                            x.ContractNumber,
                            x.StartDate,
                            x.EndDate,
                            x.Status,
                            x.RetainerAmount,
                            x.Terms))
                .Skip(offset)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

        return new GetClientContractsResponseDto(items);
    }
}