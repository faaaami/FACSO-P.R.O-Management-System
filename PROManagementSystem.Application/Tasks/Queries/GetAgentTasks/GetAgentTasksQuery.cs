using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTasks;

public sealed record GetAgentTasksQuery(
    RenewalTaskStatus? Status
) : IRequest<GetRenewalTasksResponseDto>;
