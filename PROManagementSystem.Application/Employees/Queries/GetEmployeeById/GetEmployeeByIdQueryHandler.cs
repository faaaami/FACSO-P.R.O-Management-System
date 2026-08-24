using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

public sealed class GetEmployeeByIdQueryHandler
    : IRequestHandler<
        GetEmployeeByIdQuery,
        GetEmployeeByIdResponseDto?>
{
    private readonly IEmployeeReadRepository _repository;

    public GetEmployeeByIdQueryHandler(
        IEmployeeReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetEmployeeByIdResponseDto?> Handle(
        GetEmployeeByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(
            request.EmployeeId,
            request.IncludeDeleted,
            cancellationToken);
    }
}