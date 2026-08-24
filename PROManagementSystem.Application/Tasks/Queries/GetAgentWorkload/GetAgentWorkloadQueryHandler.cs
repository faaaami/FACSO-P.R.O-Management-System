using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentWorkload;

public sealed class GetAgentWorkloadQueryHandler
    : IRequestHandler<
        GetAgentWorkloadQuery,
        GetStaffWorkloadResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetAgentWorkloadQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetStaffWorkloadResponseDto?> Handle(
        GetAgentWorkloadQuery request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        return await _repository.GetStaffWorkloadAsync(
            agentId,
            cancellationToken);
    }
}
