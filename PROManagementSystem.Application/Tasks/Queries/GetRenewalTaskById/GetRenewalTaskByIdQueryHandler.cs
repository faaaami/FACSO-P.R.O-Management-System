using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

public sealed class GetRenewalTaskByIdQueryHandler
    : IRequestHandler<
        GetRenewalTaskByIdQuery,
        GetRenewalTaskByIdResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;

    public GetRenewalTaskByIdQueryHandler(
        IRenewalTaskReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetRenewalTaskByIdResponseDto?> Handle(
        GetRenewalTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(
            request.TaskId,
            cancellationToken);
    }
}