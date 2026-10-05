using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTasks;

public sealed class GetMyRenewalTasksQueryHandler
    : IRequestHandler<
        GetMyRenewalTasksQuery,
        GetRenewalTasksResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyRenewalTasksQueryHandler(
        IRenewalTaskReadRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTasksResponseDto> Handle(
        GetMyRenewalTasksQuery request,
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

        return await _repository.GetTasksAsync(
            request.Status,
            null,
            clientCompanyId.Value,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}