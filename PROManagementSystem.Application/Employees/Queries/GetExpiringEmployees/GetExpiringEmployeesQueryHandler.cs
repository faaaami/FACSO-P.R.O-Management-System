using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetExpiringEmployees;

public sealed class GetExpiringEmployeesQueryHandler
    : IRequestHandler<
        GetExpiringEmployeesQuery,
        GetExpiringEmployeesResponseDto>
{
    private readonly IEmployeeExpiryReadRepository _repository;

    public GetExpiringEmployeesQueryHandler(
        IEmployeeExpiryReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetExpiringEmployeesResponseDto> Handle(
        GetExpiringEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetExpiringAsync(
            request.Days,
            request.Page,
            request.PageSize,
            request.IncludeExpired,
            cancellationToken);
    }
}