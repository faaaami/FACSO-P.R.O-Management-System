using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

namespace PROManagementSystem.Application.Client.Queries.GetMyEmployeeById;

public sealed class GetMyEmployeeByIdQueryHandler
    : IRequestHandler<GetMyEmployeeByIdQuery, GetEmployeeByIdResponseDto?>
{
    private readonly IEmployeeReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyEmployeeByIdQueryHandler(
        IEmployeeReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetEmployeeByIdResponseDto?> Handle(
        GetMyEmployeeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // The tenant restriction runs inside the SQL query, so an
        // out-of-company employee resolves to null -> 404.
        return await _repository.GetByIdForCompanyAsync(
            clientCompanyId.Value,
            request.EmployeeId,
            cancellationToken);
    }
}