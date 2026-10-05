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

        // Strict task-linked scope check on the parent client company.
        // Out-of-scope or nonexistent parent -> 404, never an empty list.
        var clientInScope =
            await _repository.IsAgentClientInScopeAsync(
                agentId,
                request.ClientId,
                cancellationToken);

        if (!clientInScope)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _repository.GetAgentClientEntitiesAsync(
            agentId,
            request.ClientId,
            request.Page,
            request.PageSize,
            request.Search,
            cancellationToken);
    }
}
