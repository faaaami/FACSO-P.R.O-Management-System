using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;

namespace PROManagementSystem.Application.Client.Queries.GetMyServiceRequest;

public sealed class GetMyServiceRequestQueryHandler
    : IRequestHandler<
        GetMyServiceRequestQuery,
        GetServiceRequestResponseDto?>
{
    private readonly IServiceRequestReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyServiceRequestQueryHandler(
        IServiceRequestReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetServiceRequestResponseDto?> Handle(
        GetMyServiceRequestQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _repository.GetServiceRequestByIdForCompanyAsync(
            request.Id,
            clientCompanyId.Value,
            cancellationToken);
    }
}