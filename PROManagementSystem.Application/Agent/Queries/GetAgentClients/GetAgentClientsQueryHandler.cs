using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClients;

public sealed class GetAgentClientsQueryHandler
    : IRequestHandler<
        GetAgentClientsQuery,
        GetClientCompaniesResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentClientsQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetClientCompaniesResponseDto> Handle(
        GetAgentClientsQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentClientCompaniesAsync(
            agentId,
            request.Page,
            request.PageSize,
            request.Search,
            cancellationToken);
    }
}
