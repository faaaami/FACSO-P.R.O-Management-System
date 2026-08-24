using PROManagementSystem.Application.Staff.Commands.CreateStaff;

namespace PROManagementSystem.Api.Controllers.Staff;

public sealed record CreateStaffRequestDto(
    string FullName,
    string Email,
    string Password,
    string? Phone
);