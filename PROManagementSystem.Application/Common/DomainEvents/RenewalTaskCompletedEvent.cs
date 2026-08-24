using MediatR;

namespace PROManagementSystem.Application.Common.DomainEvents;

public sealed record RenewalTaskCompletedEvent(
    Guid RenewalTaskId,
    Guid ClientCompanyId
) : INotification;
