using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentEmployeeDocuments;

public sealed class GetAgentEmployeeDocumentsQueryHandler
    : IRequestHandler<
        GetAgentEmployeeDocumentsQuery,
        GetEmployeeDocumentsResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentEmployeeDocumentsQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetEmployeeDocumentsResponseDto> Handle(
        GetAgentEmployeeDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // Strict task-linked scope check on the parent employee.
        // Out-of-scope or nonexistent parent -> 404, never an empty list.
        var employeeInScope =
            await _repository.IsAgentEmployeeInScopeAsync(
                agentId,
                request.EmployeeId,
                cancellationToken);

        if (!employeeInScope)
        {
            throw new KeyNotFoundException(
                "Employee was not found.");
        }

        return await _repository.GetAgentEmployeeDocumentsAsync(
            agentId,
            request.EmployeeId,
            cancellationToken);
    }
}
