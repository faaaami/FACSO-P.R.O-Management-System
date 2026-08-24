using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.RejectServiceRequest;

public sealed record RejectServiceRequestResponseDto(
    Guid ServiceRequestId,
    ServiceRequestStatus Status,
    string RejectionReason,
    DateTime RejectedAt
);