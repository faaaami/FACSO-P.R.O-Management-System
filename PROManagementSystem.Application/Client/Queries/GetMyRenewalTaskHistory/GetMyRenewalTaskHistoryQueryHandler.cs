using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskHistory;

public sealed class GetMyRenewalTaskHistoryQueryHandler
    : IRequestHandler<
        GetMyRenewalTaskHistoryQuery,
        GetRenewalTaskHistoryResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyRenewalTaskHistoryQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTaskHistoryResponseDto?> Handle(
        GetMyRenewalTaskHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _repository.GetHistoryForCompanyAsync(
            request.TaskId,
            clientCompanyId.Value,
            cancellationToken);
    }
}