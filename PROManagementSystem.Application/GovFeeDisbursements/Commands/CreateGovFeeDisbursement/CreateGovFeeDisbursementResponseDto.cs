using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.CreateGovFeeDisbursement;

public sealed record CreateGovFeeDisbursementResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid? RenewalTaskId,
    string FeeDescription,
    string? GovernmentReference,
    decimal Amount,
    string Currency,
    GovFeeDisbursementStatus Status,
    DateTime PaidByFirmAt,
    DateTime? InvoicedAt,
    DateTime? ReimbursedAt,
    string? Notes,
    DateTime CreatedAt
);
