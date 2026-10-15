using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Queries.GetServiceFeeInvoices;

public sealed record GetServiceFeeInvoiceListItemDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal Amount,
    string Currency,
    ServiceFeeInvoiceStatus Status,
    string? Description,
    DateTime? PaidAt,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt
);

public sealed record GetServiceFeeInvoicesResponseDto(
    IReadOnlyList<GetServiceFeeInvoiceListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);
