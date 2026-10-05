using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

public sealed record GetServiceRequestsResponseDto(
    List<ServiceRequestListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed record ServiceRequestListItemDto(
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
    DateTime CreatedAt
);