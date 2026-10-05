using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Commands.CreateServiceRequest;

public sealed record CreateServiceRequestRequest(
    ServiceRequestType Type,
    Guid? EmployeeId,
    Guid? EntityId,
    string? Notes
);