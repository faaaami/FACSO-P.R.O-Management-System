using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentEmployees;

public sealed class GetAgentEmployeesQueryHandler
    : IRequestHandler<
        GetAgentEmployeesQuery,
        GetEmployeesResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentEmployeesQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetEmployeesResponseDto> Handle(
        GetAgentEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentEmployeesAsync(
            agentId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
