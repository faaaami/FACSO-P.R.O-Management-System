using MediatR;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.VoidServiceFeeInvoice;

public sealed record VoidServiceFeeInvoiceCommand(
    Guid Id,
    string? Reason
) : IRequest<VoidServiceFeeInvoiceResponseDto>;
