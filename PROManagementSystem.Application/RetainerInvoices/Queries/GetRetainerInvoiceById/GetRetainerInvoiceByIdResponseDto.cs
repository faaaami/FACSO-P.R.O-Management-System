using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Queries.GetRetainerInvoiceById;

public sealed record GetRetainerInvoiceByIdResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ClientCompanyName,
    Guid ServiceContractId,
    string ContractNumber,
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
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? PaymentOrderId,
    PaymentStatus? PaymentOrderStatus,
    string? RazorpayOrderId
);
