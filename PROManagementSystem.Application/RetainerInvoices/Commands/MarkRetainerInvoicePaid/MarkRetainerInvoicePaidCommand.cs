using MediatR;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.MarkRetainerInvoicePaid;

public sealed record MarkRetainerInvoicePaidCommand(
    Guid Id
) : IRequest<MarkRetainerInvoicePaidResponseDto>;
