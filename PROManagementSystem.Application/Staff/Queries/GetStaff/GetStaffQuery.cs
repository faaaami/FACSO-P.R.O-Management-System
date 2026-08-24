using MediatR;

namespace PROManagementSystem.Application.Staff.Queries.GetStaff;

public sealed record GetStaffQuery(
    bool IncludeInactive = false
) : IRequest<GetStaffResponseDto>;