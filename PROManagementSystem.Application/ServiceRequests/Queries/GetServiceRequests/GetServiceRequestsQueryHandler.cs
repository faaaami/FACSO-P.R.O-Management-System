using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

public sealed class GetServiceRequestsQueryHandler
    : IRequestHandler<
        GetServiceRequestsQuery,
        GetServiceRequestsResponseDto>
{
    private readonly IServiceRequestReadRepository _repository;

    public GetServiceRequestsQueryHandler(
        IServiceRequestReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetServiceRequestsResponseDto> Handle(
        GetServiceRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ArgumentException(
                "Page must be greater than or equal to 1.");
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ArgumentException(
                "PageSize must be between 1 and 100.");
        }

        return await _repository.GetServiceRequestsAsync(
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}