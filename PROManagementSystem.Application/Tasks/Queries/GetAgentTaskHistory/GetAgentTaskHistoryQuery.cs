using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTaskHistory;

public sealed record GetAgentTaskHistoryQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskHistoryResponseDto?>;
