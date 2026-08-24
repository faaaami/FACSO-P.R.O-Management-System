using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployees;

public sealed class GetEmployeesQueryHandler
    : IRequestHandler<
        GetEmployeesQuery,
        GetEmployeesResponseDto>
{
    private readonly IEmployeeReadRepository _repository;

    public GetEmployeesQueryHandler(
        IEmployeeReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetEmployeesResponseDto> Handle(
        GetEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetPagedAsync(
            request.ClientId,
            request.EntityId,
            request.IsActive,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}