using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;

namespace PROManagementSystem.Application.Tasks.Queries.GetAgentTaskSteps;

public sealed record GetAgentTaskStepsQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskStepsResponseDto?>;
