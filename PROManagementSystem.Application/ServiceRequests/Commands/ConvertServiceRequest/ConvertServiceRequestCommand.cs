using MediatR;

namespace PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequest;

public sealed record ConvertServiceRequestCommand(
    Guid ServiceRequestId
) : IRequest<ConvertServiceRequestResponseDto>;