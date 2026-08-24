using MediatR;

namespace PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

public sealed record GetStaffWorkloadQuery(
    Guid StaffId
) : IRequest<GetStaffWorkloadResponseDto?>;