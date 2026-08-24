using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;

public sealed record GetRenewalTasksQuery(
    RenewalTaskStatus? Status,
    Guid? AssignedStaffId,
    Guid? ClientCompanyId
) : IRequest<GetRenewalTasksResponseDto>;