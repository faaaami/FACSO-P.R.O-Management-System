using MediatR;

namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

public sealed record GetRenewalTaskByIdQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskByIdResponseDto?>;