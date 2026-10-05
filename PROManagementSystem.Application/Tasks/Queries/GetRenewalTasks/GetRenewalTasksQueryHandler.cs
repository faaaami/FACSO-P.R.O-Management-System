using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;

public sealed class GetRenewalTasksQueryHandler
    : IRequestHandler<
        GetRenewalTasksQuery,
        GetRenewalTasksResponseDto>
{
    private readonly IRenewalTaskReadRepository _repository;

    public GetRenewalTasksQueryHandler(
        IRenewalTaskReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRenewalTasksResponseDto> Handle(
        GetRenewalTasksQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetTasksAsync(
            request.Status,
            request.AssignedStaffId,
            request.ClientCompanyId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}