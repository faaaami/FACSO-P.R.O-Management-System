using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTasks;

public sealed record GetMyRenewalTasksQuery(
    RenewalTaskStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetRenewalTasksResponseDto>;