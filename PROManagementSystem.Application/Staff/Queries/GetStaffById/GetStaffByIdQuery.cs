using MediatR;

namespace PROManagementSystem.Application.Staff.Queries.GetStaffById;

public sealed record GetStaffByIdQuery(
    Guid StaffId
) : IRequest<GetStaffByIdResponseDto?>;