using MediatR;

namespace PROManagementSystem.Application.Staff.Commands.UpdateStaff;

public sealed record UpdateStaffCommand(
    Guid StaffId,
    string FullName,
    string? Email,
    string? Phone,
    bool? IsApproved
) : IRequest<UpdateStaffResponseDto>;