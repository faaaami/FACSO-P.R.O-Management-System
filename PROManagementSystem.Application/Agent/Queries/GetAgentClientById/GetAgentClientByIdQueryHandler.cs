using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentClientById;

public sealed class GetAgentClientByIdQueryHandler
    : IRequestHandler<
        GetAgentClientByIdQuery,
        GetClientCompanyByIdResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentClientByIdQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetClientCompanyByIdResponseDto?> Handle(
        GetAgentClientByIdQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentClientCompanyByIdAsync(
            agentId,
            request.ClientId,
            cancellationToken);
    }
}
