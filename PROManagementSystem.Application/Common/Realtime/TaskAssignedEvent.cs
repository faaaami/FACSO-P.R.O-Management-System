namespace PROManagementSystem.Application.Common.Realtime;

public sealed record TaskAssignedEvent(
    Guid TaskId,
    Guid StaffId,
    string StaffName,
    Guid DocumentId,
    string DocumentNumber
);