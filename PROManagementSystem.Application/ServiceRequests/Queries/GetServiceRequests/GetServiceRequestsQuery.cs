using MediatR;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

public sealed record GetServiceRequestsQuery(
    int Page = 1,
    int PageSize = 20
) : IRequest<GetServiceRequestsResponseDto>;