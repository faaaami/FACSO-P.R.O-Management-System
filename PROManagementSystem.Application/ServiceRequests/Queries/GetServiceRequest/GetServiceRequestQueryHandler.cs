using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;

public sealed class GetServiceRequestQueryHandler
    : IRequestHandler<
        GetServiceRequestQuery,
        GetServiceRequestResponseDto>
{
    private readonly IServiceRequestReadRepository _repository;

    public GetServiceRequestQueryHandler(
        IServiceRequestReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetServiceRequestResponseDto> Handle(
        GetServiceRequestQuery request,
        CancellationToken cancellationToken)
    {
        var result =
            await _repository.GetServiceRequestByIdAsync(
                request.Id,
                cancellationToken);

        if (result is null)
        {
            throw new KeyNotFoundException(
                "Service request was not found.");
        }

        return result;
    }
}