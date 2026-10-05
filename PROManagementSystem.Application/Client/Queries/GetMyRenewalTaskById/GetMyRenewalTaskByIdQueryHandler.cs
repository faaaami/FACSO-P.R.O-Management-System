using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskById;

public sealed class GetMyRenewalTaskByIdQueryHandler
    : IRequestHandler<
        GetMyRenewalTaskByIdQuery,
        GetRenewalTaskByIdResponseDto?>
{
    private readonly IRenewalTaskReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyRenewalTaskByIdQueryHandler(
        IRenewalTaskReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetRenewalTaskByIdResponseDto?> Handle(
        GetMyRenewalTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _repository.GetByIdForCompanyAsync(
            request.TaskId,
            clientCompanyId.Value,
            cancellationToken);
    }
}