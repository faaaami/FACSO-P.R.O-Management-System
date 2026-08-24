using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTaskById;

public sealed record GetAgentTaskByIdQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskByIdResponseDto?>;
