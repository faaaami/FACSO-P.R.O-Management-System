using MediatR;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;

namespace PROManagementSystem.Application.Client.Queries.GetMyRenewalTaskById;

public sealed record GetMyRenewalTaskByIdQuery(
    Guid TaskId
) : IRequest<GetRenewalTaskByIdResponseDto?>;