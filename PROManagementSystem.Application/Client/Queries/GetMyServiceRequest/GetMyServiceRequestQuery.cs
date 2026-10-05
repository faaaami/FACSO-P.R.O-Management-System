using MediatR;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;

namespace PROManagementSystem.Application.Client.Queries.GetMyServiceRequest;

public sealed record GetMyServiceRequestQuery(
    Guid Id
) : IRequest<GetServiceRequestResponseDto?>;