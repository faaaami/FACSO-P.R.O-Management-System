using MediatR;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;

public sealed record GetServiceRequestQuery(
    Guid Id
) : IRequest<GetServiceRequestResponseDto>;