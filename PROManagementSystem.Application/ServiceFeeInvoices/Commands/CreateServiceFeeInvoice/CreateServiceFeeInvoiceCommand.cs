using MediatR;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed record CreateServiceFeeInvoiceCommand(
    Guid ClientCompanyId,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal Amount,
    string Currency,
    string? Description
) : IRequest<CreateServiceFeeInvoiceResponseDto>;
