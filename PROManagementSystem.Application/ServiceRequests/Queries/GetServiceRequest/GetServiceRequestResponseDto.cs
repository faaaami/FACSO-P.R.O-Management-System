using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;

public sealed record GetServiceRequestResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid DocumentId,
    ServiceRequestStatus Status,
    string? Description,
    string? RejectionReason,
    DateTime? ConvertedAt,
    DateTime? RejectedAt,
    Guid? ConvertedRenewalTaskId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);