using MediatR;

namespace PROManagementSystem.Application.Staff.Queries.GetStaff;

public sealed record GetStaffQuery(
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetStaffResponseDto>;