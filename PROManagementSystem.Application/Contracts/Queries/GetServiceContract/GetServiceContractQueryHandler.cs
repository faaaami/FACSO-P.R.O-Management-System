using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Contracts.Queries.GetServiceContract;

public sealed class GetServiceContractQueryHandler
    : IRequestHandler<
        GetServiceContractQuery,
        GetServiceContractResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetServiceContractQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetServiceContractResponseDto> Handle(
        GetServiceContractQuery request,
        CancellationToken cancellationToken)
    {
        var contract =
            await _context.ServiceContracts
                .AsNoTracking()
                .Where(
                    x =>
                        x.Id == request.Id &&
                        !x.IsDeleted)
                .Select(
                    x =>
                        new GetServiceContractResponseDto(
                            x.Id,
                            x.ClientCompanyId,
                            x.ContractNumber,
                            x.StartDate,
                            x.EndDate,
                            x.Status,
                            x.RetainerAmount,
                            x.Terms,
                            x.CreatedAt,
                            x.UpdatedAt))
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (contract is null)
        {
            throw new KeyNotFoundException(
                "Service contract was not found.");
        }

        return contract;
    }
}