using MediatR;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;

public sealed record GetRenewalTaskStepsQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskStepsResponseDto?>;