using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTaskById;

public sealed class GetAgentTaskByIdQueryHandler
    : IRequestHandler<
        GetAgentTaskByIdQuery,
        GetRenewalTaskByIdResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentTaskByIdQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTaskByIdResponseDto?> Handle(
        GetAgentTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentTaskByIdAsync(
            request.TaskId,
            agentId,
            cancellationToken);
    }
}
