using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTaskHistory;

public sealed class GetAgentTaskHistoryQueryHandler
    : IRequestHandler<
        GetAgentTaskHistoryQuery,
        GetRenewalTaskHistoryResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentTaskHistoryQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTaskHistoryResponseDto?> Handle(
        GetAgentTaskHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentHistoryAsync(
            request.TaskId,
            agentId,
            cancellationToken);
    }
}
