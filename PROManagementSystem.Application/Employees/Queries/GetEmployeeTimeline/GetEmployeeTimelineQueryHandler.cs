using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;

public sealed class GetEmployeeTimelineQueryHandler
    : IRequestHandler<
        GetEmployeeTimelineQuery,
        GetEmployeeTimelineResponseDto>
{
    private readonly IEmployeeTimelineReadRepository _repository;

    public GetEmployeeTimelineQueryHandler(
        IEmployeeTimelineReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetEmployeeTimelineResponseDto> Handle(
        GetEmployeeTimelineQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetTimelineAsync(
            request.EmployeeId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}