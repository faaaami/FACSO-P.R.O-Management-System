namespace PROManagementSystem.Api.Controllers.Staff;

public sealed record UpdateStaffRequestDto(
    string FullName,
    string Email,
    string? Phone,
    bool IsApproved
);