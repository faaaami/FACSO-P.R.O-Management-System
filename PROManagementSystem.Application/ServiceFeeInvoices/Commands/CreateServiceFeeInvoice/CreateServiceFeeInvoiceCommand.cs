using MediatR;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.CreateServiceFeeInvoice;

public sealed record CreateServiceFeeInvoiceCommand(
    Guid ClientCompanyId,
    Guid RenewalTaskId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Amount,
    string Currency,
    string? Description
) : IRequest<CreateServiceFeeInvoiceResponseDto>;
