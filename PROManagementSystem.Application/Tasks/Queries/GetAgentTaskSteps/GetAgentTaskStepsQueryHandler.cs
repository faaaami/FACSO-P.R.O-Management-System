using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTaskSteps;

public sealed class GetAgentTaskStepsQueryHandler
    : IRequestHandler<
        GetAgentTaskStepsQuery,
        GetRenewalTaskStepsResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentTaskStepsQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTaskStepsResponseDto?> Handle(
        GetAgentTaskStepsQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentStepsAsync(
            request.TaskId,
            agentId,
            cancellationToken);
    }
}
