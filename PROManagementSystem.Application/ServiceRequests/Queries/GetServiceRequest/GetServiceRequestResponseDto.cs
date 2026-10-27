using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;

public sealed record GetServiceRequestResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    ServiceRequestType Type,
    Guid? EmployeeId,
    Guid? EntityId,
    Guid? DocumentId,
    ServiceRequestStatus Status,
    string? Description,
    string? RejectionReason,
    DateTime? ConvertedAt,
    DateTime? RejectedAt,
    Guid? ConvertedRenewalTaskId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? ServiceDefinitionId,
    string? ServiceName,
    Guid? ConvertedServiceJobId,
    string? ConvertedJobNumber
);