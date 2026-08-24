using MediatR;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentWorkload;

public sealed record GetAgentWorkloadQuery()
    : IRequest<GetStaffWorkloadResponseDto?>;
