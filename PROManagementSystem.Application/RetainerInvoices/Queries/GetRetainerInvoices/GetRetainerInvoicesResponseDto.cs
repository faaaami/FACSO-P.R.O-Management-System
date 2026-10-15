using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoices;

public sealed record GetRetainerInvoiceListItemDto(
    Guid Id,
    Guid ClientCompanyId,
    Guid ServiceContractId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    decimal Amount,
    string Currency,
    RetainerInvoiceStatus Status,
    string? Notes,
    DateTime? PaidAt,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt
);

public sealed record GetRetainerInvoicesResponseDto(
    IReadOnlyList<GetRetainerInvoiceListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);
