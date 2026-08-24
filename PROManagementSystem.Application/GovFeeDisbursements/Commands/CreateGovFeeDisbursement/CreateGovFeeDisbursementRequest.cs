namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.CreateGovFeeDisbursement;

public sealed record CreateGovFeeDisbursementRequest(
    Guid ClientCompanyId,
    Guid? RenewalTaskId,
    string FeeDescription,
    string? GovernmentReference,
    decimal Amount,
    string Currency,
    string? Notes
);
