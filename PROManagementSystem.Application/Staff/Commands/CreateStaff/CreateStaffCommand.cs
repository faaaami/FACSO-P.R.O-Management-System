using MediatR;

namespace PROManagementSystem.Application.Staff.Commands.CreateStaff;

public sealed record CreateStaffCommand(
    string FullName,
    string Email,
    string Password,
    string? Phone
) : IRequest<CreateStaffResponseDto>;