using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskHistory;

public sealed record GetMyRenewalTaskHistoryQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskHistoryResponseDto?>;