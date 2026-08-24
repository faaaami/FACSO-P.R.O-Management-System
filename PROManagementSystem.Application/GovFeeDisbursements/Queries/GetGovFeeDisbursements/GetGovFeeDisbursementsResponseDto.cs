namespace PROManagementSystem.Application.GovFeeDisbursements.Queries.GetGovFeeDisbursements;

public sealed record GetGovFeeDisbursementsResponseDto(
    List<GovFeeDisbursementDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record GovFeeDisbursementDto(
    Guid Id,
    Guid ClientCompanyId,
    string ClientCompanyName,
    Guid? RenewalTaskId,
    string FeeDescription,
    string? GovernmentReference,
    decimal Amount,
    string Currency,
    int Status,
    DateTime PaidByFirmAt,
    DateTime? InvoicedAt,
    DateTime? ReimbursedAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
