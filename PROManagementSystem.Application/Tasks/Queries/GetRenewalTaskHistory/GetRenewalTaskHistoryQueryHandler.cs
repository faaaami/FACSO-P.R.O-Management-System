using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

public sealed class GetRenewalTaskHistoryQueryHandler
    : IRequestHandler<
        GetRenewalTaskHistoryQuery,
        GetRenewalTaskHistoryResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;

    public GetRenewalTaskHistoryQueryHandler(
        IRenewalTaskReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRenewalTaskHistoryResponseDto?> Handle(
        GetRenewalTaskHistoryQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetHistoryAsync(
            request.TaskId,
            cancellationToken);
    }
}