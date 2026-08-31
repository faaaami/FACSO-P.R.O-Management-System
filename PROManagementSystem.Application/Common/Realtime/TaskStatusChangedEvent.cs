using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Realtime;

public sealed record TaskStatusChangedEvent(
    Guid TaskId,
    Guid StaffId,
    RenewalTaskStatus PreviousStatus,
    RenewalTaskStatus Status,
    DateTime UpdatedAt
);
