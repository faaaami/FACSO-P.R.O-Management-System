using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

namespace PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;

public sealed record GetAllStaffWorkloadResponseDto(
    IReadOnlyList<GetStaffWorkloadResponseDto> Items
);