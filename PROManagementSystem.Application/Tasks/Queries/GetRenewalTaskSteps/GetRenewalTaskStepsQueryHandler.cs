using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;

public sealed class GetRenewalTaskStepsQueryHandler
    : IRequestHandler<
        GetRenewalTaskStepsQuery,
        GetRenewalTaskStepsResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;

    public GetRenewalTaskStepsQueryHandler(
        IRenewalTaskReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRenewalTaskStepsResponseDto?> Handle(
        GetRenewalTaskStepsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetStepsAsync(
            request.TaskId,
            cancellationToken);
    }
}