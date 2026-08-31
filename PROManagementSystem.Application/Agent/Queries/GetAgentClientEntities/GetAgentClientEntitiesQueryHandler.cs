using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClientEntities;

public sealed class GetAgentClientEntitiesQueryHandler
    : IRequestHandler<
        GetAgentClientEntitiesQuery,
        GetClientEntitiesResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentClientEntitiesQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetClientEntitiesResponseDto> Handle(
        GetAgentClientEntitiesQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentClientEntitiesAsync(
            agentId,
            request.ClientId,
            request.Page,
            request.PageSize,
            request.Search,
            cancellationToken);
    }
}
