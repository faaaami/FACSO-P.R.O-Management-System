using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Commands.CreateServiceRequest;

public sealed record CreateServiceRequestResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    ServiceRequestType Type,
    Guid? EmployeeId,
    Guid? EntityId,
    Guid? DocumentId,
    ServiceRequestStatus Status,
    string? Description,
    DateTime CreatedAt
);