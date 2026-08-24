using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequest;

public sealed record ConvertServiceRequestResponseDto(
    Guid ServiceRequestId,
    Guid RenewalTaskId,
    ServiceRequestStatus Status,
    DateTime ConvertedAt
);