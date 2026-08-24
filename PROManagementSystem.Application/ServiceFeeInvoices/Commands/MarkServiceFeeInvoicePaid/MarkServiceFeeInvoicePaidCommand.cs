using MediatR;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.MarkServiceFeeInvoicePaid;

public sealed record MarkServiceFeeInvoicePaidCommand(
    Guid Id
) : IRequest<MarkServiceFeeInvoicePaidResponseDto>;
