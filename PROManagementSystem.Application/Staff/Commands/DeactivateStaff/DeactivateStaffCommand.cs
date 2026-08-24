using MediatR;

namespace PROManagementSystem.Application.Staff.Commands.DeactivateStaff;

public sealed record DeactivateStaffCommand(
    Guid StaffId
) : IRequest<DeactivateStaffResponseDto>;