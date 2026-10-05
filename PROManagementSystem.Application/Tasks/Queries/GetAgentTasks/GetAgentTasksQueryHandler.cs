using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTasks;

public sealed class GetAgentTasksQueryHandler
    : IRequestHandler<
        GetAgentTasksQuery,
        GetRenewalTasksResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentTasksQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTasksResponseDto> Handle(
        GetAgentTasksQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetTasksAsync(
            request.Status,
            agentId,
            null,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
