using MediatR;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;

public sealed record GetRenewalTaskHistoryQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskHistoryResponseDto?>;