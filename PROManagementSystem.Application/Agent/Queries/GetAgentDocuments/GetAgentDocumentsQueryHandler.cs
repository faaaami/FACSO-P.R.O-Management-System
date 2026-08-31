using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;

public sealed class GetAgentDocumentsQueryHandler
    : IRequestHandler<
        GetAgentDocumentsQuery,
        GetAgentDocumentsResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentDocumentsQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetAgentDocumentsResponseDto> Handle(
        GetAgentDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetAgentDocumentsAsync(
            agentId,
            request.Type,
            request.Status,
            request.ExpiresBefore,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
