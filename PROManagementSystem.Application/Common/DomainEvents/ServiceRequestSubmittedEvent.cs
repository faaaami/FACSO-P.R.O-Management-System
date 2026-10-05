using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.DomainEvents;

public sealed record ServiceRequestSubmittedEvent(
    Guid ServiceRequestId,
    Guid ClientCompanyId,
    ServiceRequestType Type,
    Guid? EmployeeId,
    Guid? EntityId,
    Guid? DocumentId,
    string? Description
) : INotification;