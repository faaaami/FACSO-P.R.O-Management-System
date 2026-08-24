using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Queries.GetActiveContract;

public sealed class GetActiveContractQueryHandler
    : IRequestHandler<
        GetActiveContractQuery,
        GetActiveContractResponseDto?>
{
    private readonly IApplicationDbContext _context;

    public GetActiveContractQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetActiveContractResponseDto?> Handle(
        GetActiveContractQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.ServiceContracts
            .AsNoTracking()
            .Where(
                x =>
                    x.ClientCompanyId ==
                        request.ClientCompanyId &&
                    x.Status ==
                        ServiceContractStatus.Active &&
                    !x.IsDeleted)
            .Select(
                x =>
                    new GetActiveContractResponseDto(
                        x.Id,
                        x.ClientCompanyId,
                        x.ContractNumber,
                        x.StartDate,
                        x.EndDate,
                        x.Status,
                        x.RetainerAmount,
                        x.Terms))
            .FirstOrDefaultAsync(
                cancellationToken);
    }
}