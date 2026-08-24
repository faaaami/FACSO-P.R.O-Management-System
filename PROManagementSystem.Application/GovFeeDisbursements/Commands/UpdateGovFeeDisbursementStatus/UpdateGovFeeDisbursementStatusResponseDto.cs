using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.UpdateGovFeeDisbursementStatus;

public sealed record UpdateGovFeeDisbursementStatusResponseDto(
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);
