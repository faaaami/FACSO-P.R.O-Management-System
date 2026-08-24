namespace PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

public sealed record GetStaffWorkloadResponseDto(
    Guid StaffId,
    int ActiveTaskCount,
    int BlockedTaskCount,
    IReadOnlyDictionary<string, int> TasksByStatus
);