using MediatR;

namespace PROManagementSystem.Application.ServiceRequests.Commands.RejectServiceRequest;

public sealed record RejectServiceRequestCommand(
    Guid ServiceRequestId,
    string Reason
) : IRequest<RejectServiceRequestResponseDto>;