using MediatR;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyServiceRequests;

public sealed record GetMyServiceRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    ServiceRequestStatus? Status = null
) : IRequest<GetServiceRequestsResponseDto>;