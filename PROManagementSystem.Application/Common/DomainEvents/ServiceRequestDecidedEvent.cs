using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.DomainEvents;

/// <summary>
/// Raised once a service request reaches a terminal decision, so the client
/// company can be told that its request was converted into a renewal task or
/// rejected with a reason.
/// </summary>
public sealed record ServiceRequestDecidedEvent(
    Guid ServiceRequestId,
    Guid ClientCompanyId,
    ServiceRequestStatus Status,
    Guid? ConvertedRenewalTaskId,
    string? RejectionReason
) : INotification;
