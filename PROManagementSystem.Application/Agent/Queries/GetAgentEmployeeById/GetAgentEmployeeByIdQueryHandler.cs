using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentEmployeeById;

public sealed class GetAgentEmployeeByIdQueryHandler
    : IRequestHandler<
        GetAgentEmployeeByIdQuery,
        GetEmployeeByIdResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentEmployeeByIdQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetEmployeeByIdResponseDto?> Handle(
        GetAgentEmployeeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentEmployeeByIdAsync(
            agentId,
            request.EmployeeId,
            cancellationToken);
    }
}
