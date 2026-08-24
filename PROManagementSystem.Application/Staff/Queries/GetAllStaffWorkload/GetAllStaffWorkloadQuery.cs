using MediatR;

namespace PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;

public sealed record GetAllStaffWorkloadQuery
    : IRequest<GetAllStaffWorkloadResponseDto>;