using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursementById;

public sealed record GetGovFeeDisbursementByIdResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ClientCompanyName,
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
