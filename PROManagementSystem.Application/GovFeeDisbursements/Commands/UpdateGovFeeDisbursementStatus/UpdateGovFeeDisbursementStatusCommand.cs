using MediatR;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.UpdateGovFeeDisbursementStatus;

public sealed record UpdateGovFeeDisbursementStatusCommand(
    Guid Id,
    GovFeeDisbursementStatus Status
) : IRequest<UpdateGovFeeDisbursementStatusResponseDto>;
