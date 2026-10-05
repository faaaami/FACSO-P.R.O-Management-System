using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

namespace PROManagementSystem.Application.Client.Queries.GetMyServiceRequests;

public sealed class GetMyServiceRequestsQueryHandler
    : IRequestHandler<
        GetMyServiceRequestsQuery,
        GetServiceRequestsResponseDto>
{
    private readonly IServiceRequestReadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyServiceRequestsQueryHandler(
        IServiceRequestReadRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetServiceRequestsResponseDto> Handle(
        GetMyServiceRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

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

        return await _repository.GetServiceRequestsByCompanyAsync(
            clientCompanyId.Value,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}