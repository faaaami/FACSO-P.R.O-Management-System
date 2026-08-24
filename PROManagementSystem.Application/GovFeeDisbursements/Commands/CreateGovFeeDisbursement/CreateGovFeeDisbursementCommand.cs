using MediatR;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.CreateGovFeeDisbursement;

public sealed record CreateGovFeeDisbursementCommand(
    Guid ClientCompanyId,
    Guid? RenewalTaskId,
    string FeeDescription,
    string? GovernmentReference,
    decimal Amount,
    string Currency,
    string? Notes
) : IRequest<CreateGovFeeDisbursementResponseDto>;
