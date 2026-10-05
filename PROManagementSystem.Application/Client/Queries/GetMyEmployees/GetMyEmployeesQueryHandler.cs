using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Application.Client.Queries.GetMyEmployees;

public sealed class GetMyEmployeesQueryHandler
    : IRequestHandler<GetMyEmployeesQuery, GetEmployeesResponseDto>
{
    private readonly IEmployeeReadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyEmployeesQueryHandler(
        IEmployeeReadRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetEmployeesResponseDto> Handle(
        GetMyEmployeesQuery request,
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

        // The tenant (ClientCompanyId) is passed into the repository query so
        // the restriction is enforced inside SQL. The entityId is a pure
        // SQL-level filter: an out-of-company entity yields an empty result
        // and never reveals whether the entity exists.
        return await _repository.GetPagedAsync(
            clientCompanyId.Value,
            request.EntityId,
            isActive: null,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}