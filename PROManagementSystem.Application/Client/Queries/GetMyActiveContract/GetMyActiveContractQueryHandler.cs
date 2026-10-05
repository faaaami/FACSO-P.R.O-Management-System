using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Contracts.Queries.GetActiveContract;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyActiveContract;

public sealed class GetMyActiveContractQueryHandler
    : IRequestHandler<GetMyActiveContractQuery, GetActiveContractResponseDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyActiveContractQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetActiveContractResponseDto?> Handle(
        GetMyActiveContractQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _context.ServiceContracts
            .AsNoTracking()
            .Where(
                x =>
                    x.ClientCompanyId == clientCompanyId.Value &&
                    x.Status == ServiceContractStatus.Active &&
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